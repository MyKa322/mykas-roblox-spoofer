using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using MyKa.Spoofer.Identity;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class IdentitySystemTests : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "TestData", "identity-" + Guid.NewGuid().ToString("N"));
    private const string OldGuid = "e629e9b7-30a7-4212-8e52-aed24b7b1aa4";
    private const string NewGuid = "7896ca84-c197-480f-98f4-5d5b00375162";
    private const string Adapter = "e49d34b8-2755-4091-84b0-605f04cc0669";

    private static IdentityValue Value(string key, string? value = "one", string? problem = null)
        => new(key, key + " label", value, "Test observation", problem);

    private static IdentitySnapshot Snapshot(params IdentityValue[] values)
        => new(1, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), "test-computer", values.Length == 0 ? [Value("machine-guid", OldGuid)] : values);

    private static IdentityBackup Backup() => new(1, "0123456789abcdef0123456789abcdef", "S-1-5-21-1234", OldGuid,
        Snapshot(), new(true, true, false, false, true, false),
        [new(IdentitySetting.MachineGuid, null, OldGuid, NewGuid), new(IdentitySetting.AdapterAddress, Adapter, null, "02AABBCCDDEE")], ["logs", "http"], null);

    private string FilePath(string name)
    {
        Directory.CreateDirectory(root);
        return Path.Combine(root, name);
    }

    [Fact]
    public void CompareSeparatesActualChangesUnchangedAndUnavailable()
    {
        var before = Snapshot(Value("a"), Value("b"), Value("c", null, "Access unavailable"));
        var after = Snapshot(Value("a", "two"), Value("b"), Value("c", null, "Access unavailable"));
        var differences = IdentitySnapshots.Compare(before, after);
        Assert.Equal([DifferenceKind.Changed, DifferenceKind.Unchanged, DifferenceKind.Unavailable], differences.Select(v => v.Kind));
    }

    [Fact]
    public void MissingRowsAreNotClaimedRemovedWhenInventoryIsIncomplete()
    {
        var differences = IdentitySnapshots.Compare(Snapshot(Value("mac/a")), Snapshot(Value("adapters", null, "Access unavailable")));
        Assert.All(differences, v => Assert.Equal(DifferenceKind.Unavailable, v.Kind));
    }

    [Fact]
    public void CompleteInventoriesCanReportAdditionsAndRemovals()
        => Assert.Equal([DifferenceKind.Removed, DifferenceKind.Added],
            IdentitySnapshots.Compare(Snapshot(Value("a")), Snapshot(Value("b"))).Select(v => v.Kind));

    [Fact]
    public void DifferentComputersCannotBeCompared()
        => Assert.Throws<InvalidDataException>(() => IdentitySnapshots.Compare(Snapshot(), Snapshot() with { Computer = "other" }));

    [Theory]
    [InlineData("one", "bad")]
    [InlineData(null, null)]
    [InlineData("\u001bsecret", null)]
    [InlineData("", null)]
    public void SnapshotEntriesHaveExactlyOneUsableValueOrProblem(string? value, string? problem)
        => Assert.Throws<InvalidDataException>(() => IdentitySnapshots.Validate(Snapshot(Value("a", value, problem))));

    [Fact]
    public void DuplicateKeysCannotHideAValueInComparison()
        => Assert.Throws<InvalidDataException>(() => IdentitySnapshots.Validate(Snapshot(Value("a"), Value("a", "hidden"))));

    [Fact]
    public void SnapshotFilesRoundTripAndNeverOverwriteExistingFiles()
    {
        var path = FilePath("before.json");
        IdentityBackupFiles.WriteNew(path, Snapshot());
        Assert.Equal(OldGuid, Assert.Single(IdentitySnapshots.Read(path).Values).Value);
        var original = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() => IdentityBackupFiles.WriteNew(path, Snapshot(Value("a", "replacement"))));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void SnapshotExportHasProtectedPermissions()
    {
        var path = FilePath("private.json");
        IdentityBackupFiles.WriteNew(path, Snapshot());
        Assert.True(new FileInfo(path).GetAccessControl().AreAccessRulesProtected);
    }

    [Fact]
    public void BackupRoundTripsOnlyKnownSettingAndCacheTargets()
    {
        var path = FilePath("recovery.json");
        IdentityBackupFiles.WriteNew(path, Backup());
        var loaded = IdentityBackupFiles.Read(path);
        Assert.Equal(Backup().Settings, loaded.Settings);
        Assert.Equal(["logs", "http"], loaded.Caches);
    }

    [Fact]
    public void ForeignSchemaIsRefused()
        => Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(Backup() with { SchemaVersion = 2 }));

    [Theory]
    [InlineData("../profiles")]
    [InlineData("Versions")]
    [InlineData("C:\\Windows")]
    [InlineData("logs/../http")]
    public void BackupCannotIntroduceAnArbitraryCleanupPath(string path)
        => Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(Backup() with { Caches = [path] }));

    [Fact]
    public void DuplicateOrUnselectedTargetsAreRefused()
    {
        var backup = Backup();
        Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(backup with { Settings = [backup.Settings[0], backup.Settings[0]] }));
        Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(backup with { Options = backup.Options with { MachineGuid = false } }));
        Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(backup with { Settings = [new((IdentitySetting)999, null, OldGuid, NewGuid)] }));
    }

    [Theory]
    [InlineData("../../Class/0001")]
    [InlineData("not-an-adapter")]
    public void AdapterLookupCannotUseAnArbitraryRegistryPath(string id)
        => Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(Backup() with { Settings = [new(IdentitySetting.AdapterAddress, id, null, "02AABBCCDDEE")] }));

    [Fact]
    public void InvalidOriginalMacCannotBeRestored()
        => Assert.Throws<InvalidDataException>(() => IdentityBackupFiles.Validate(Backup() with { Settings = [new(IdentitySetting.AdapterAddress, Adapter, "bad-value", "02AABBCCDDEE")] }));

    [Fact]
    public void GeneratedMacOverridesAreUnicastAndLocallyAdministered()
    {
        for (var index = 0; index < 256; index++) Assert.True(MacAddress.ValidOverride(MacAddress.Create()));
    }

    [Theory]
    [InlineData("01AABBCCDDEE")]
    [InlineData("00AABBCCDDEE")]
    [InlineData("02:AA:BB:CC:DD:EE")]
    [InlineData("02AABBCCDDEG")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidOrVendorAssignedNewMacsAreRefused(string? value) => Assert.False(MacAddress.ValidOverride(value));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleValuesAreNeverOverwritten(bool restore)
    {
        var writes = 0;
        Assert.Throws<InvalidOperationException>(() => IdentityOperations.ChangeSetting(Backup().Settings[0], restore,
            () => "changed-by-another-application", _ => writes++));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ApplyAndRestoreVerifyTheWrittenValueAndPreserveMissingOverride()
    {
        var change = Backup().Settings[1];
        string? current = null;
        var writes = 0;
        void Write(string? value) { current = value; writes++; }
        Assert.True(IdentityOperations.ChangeSetting(change, false, () => current, Write));
        Assert.Equal(change.After, current);
        Assert.True(IdentityOperations.ChangeSetting(change, true, () => current, Write));
        Assert.Null(current);
        Assert.False(IdentityOperations.ChangeSetting(change, true, () => current, Write));
        Assert.Equal(2, writes);
    }

    [Fact]
    public void UnconfirmedWriteDoesNotReportSuccess()
        => Assert.Throws<IOException>(() => IdentityOperations.ChangeSetting(Backup().Settings[0], false, () => OldGuid, _ => { }));

    [Theory]
    [InlineData("hello\0", true, false, "hello")]
    [InlineData("hello", false, false, "hello")]
    [InlineData("a\0b\0", true, true, "a\0b")]
    [InlineData("\0", true, false, "")]
    public void RegSzDecodingDistinguishesTerminationAndEmbeddedNulls(string raw, bool terminated, bool embedded, string expected)
    {
        var result = RegistryIdentifiers.Decode(Encoding.Unicode.GetBytes(raw));
        Assert.Equal(new RegistryStringData(expected, terminated, embedded), result);
    }

    [Fact]
    public void OddLengthAndOversizedRegistryStringsAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => RegistryIdentifiers.Decode(new byte[3]));
        Assert.Throws<InvalidDataException>(() => RegistryIdentifiers.Decode(new byte[16386]));
    }

    [Fact]
    public void SystemRegistryTargetsAreFixedAndFirmwareMirrorsHaveNoWriteTarget()
    {
        Assert.Equal("MachineGuid", RegistryIdentifiers.Location(IdentitySetting.MachineGuid).Name);
        Assert.Equal("HwProfileGuid", RegistryIdentifiers.Location(IdentitySetting.HardwareProfileGuid).Name);
        Assert.Equal("ComputerHardwareId", RegistryIdentifiers.Location(IdentitySetting.ComputerHardwareId).Name);
        Assert.Throws<InvalidDataException>(() => RegistryIdentifiers.Location((IdentitySetting)999));
    }

    [Fact]
    public void RegistryBackupPreservesValueTypes()
    {
        var tree = new RegistryTreeBackup([
            new("text", RegistryValueKind.String, ["literal"]),
            new("path", RegistryValueKind.ExpandString, ["%LOCALAPPDATA%\\Roblox"]),
            new("many", RegistryValueKind.MultiString, ["a", "b"]),
            new("dword", RegistryValueKind.DWord, ["FFFFFFFF"]),
            new("qword", RegistryValueKind.QWord, ["FFFFFFFFFFFFFFFF"]),
            new("bytes", RegistryValueKind.Binary, ["AQID"]),
        ], []);
        RegistryTree.Validate(tree);
        Assert.Equal(-1, RegistryTree.Decode(tree.Values[3]));
        Assert.Equal(-1L, RegistryTree.Decode(tree.Values[4]));
        Assert.Equal(new byte[] { 1, 2, 3 }, RegistryTree.Decode(tree.Values[5]));
    }

    [Theory]
    [InlineData("hidden\0name")]
    [InlineData("nested\\key")]
    public void MalformedRegistryValueNamesCannotBeImported(string name)
        => Assert.Throws<InvalidDataException>(() => RegistryTree.Validate(new([new(name, RegistryValueKind.String, ["value"])], [])));

    [Fact]
    public void MalformedOrDeepRegistryBackupsAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => RegistryTree.Validate(new([new("text", RegistryValueKind.String, ["a\0b"])], [])));
        var tree = new RegistryTreeBackup([], []);
        for (var index = 0; index < 10; index++) tree = new([], new() { ["child"] = tree });
        Assert.Throws<InvalidDataException>(() => RegistryTree.Validate(tree));
    }

    [Fact]
    public void CacheMovePreservesAnExistingDestination()
    {
        var source = FilePath("source");
        var target = FilePath("target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        Assert.Throws<IOException>(() => IdentityOperations.CheckMove(source, target));
        Assert.True(Directory.Exists(source));
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void CacheAllowlistCannotBeExtendedByUserInput()
        => Assert.Throws<InvalidDataException>(() => IdentityOperations.CachePath("../Versions"));

    [Theory]
    [InlineData(null, "not confirmed")]
    [InlineData("not-json", "not confirmed")]
    [InlineData("[]", "no active")]
    [InlineData("[0,0]", "2/2")]
    [InlineData("[0,2147483647]", "1/2")]
    [InlineData("[0,1]", "1 require reboot")]
    public void DhcpResultsAreReportedWithoutClaimingANewPublicIp(string? data, string expected)
        => Assert.Contains(expected, IdentityOperations.DescribeDhcp(data));

    [Fact]
    public void SystemIdentityWindowIsDeferredUntilTheAdaptersPageDraws()
    {
        var called = 0;
        var page = new AdaptersPage(() => new([], null), () => called++);
        page.Activate();
        var result = page.Handle(Typed.Letter('s'));
        Assert.Equal(0, called);
        result.Work!();
        Assert.Equal(1, called);
    }

    [Fact]
    public void IdentityCommandLineSupportsExplicitLocalFilesAndRejectsSilentMutation()
    {
        Assert.Equal(Command.IdentityManage, CommandLine.Parse(["identity", "manage"]).Command);
        Assert.Equal("before.json", CommandLine.Parse(["identity", "snapshot", "--output", "before.json"]).OutputPath);
        var compared = CommandLine.Parse(["identity", "compare", "--before", "before.json", "--after", "after.json"]);
        Assert.Equal("before.json", compared.BeforePath);
        Assert.Equal("after.json", compared.AfterPath);
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["identity", "apply", "--all"]));
    }

    public void Dispose()
    {
        var allowed = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestData")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(root);
        if (!resolved.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup escaped its fixture directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
