# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer, Windows için bir terminal uygulamasıdır. Sürüm 0.3 ile Roblox hesaplarını ayrı giriş pencerelerinde ekleyebilir, doğrulanmış hesap bilgilerini kaydedebilir ve sistem tanılama sonuçlarını görebilirsiniz. Arayüzde Accounts, Diagnostics, Adapters ve Identity sekmeleri, klavye kontrolleri ve dönen bir 3B logo bulunur. Kimlik araçları için 4 ile Identity sekmesini açın, ardından Enter veya O tuşuna basın.

0.3 sürümünde Accounts sekmesindeki P, kayıtlı tarayıcı hesabını doğrulayıp deneyim sayfasını açar. Başlatma isteği, RobloxPlayer'ın çalıştığını veya doğru hesapla katıldığını doğrulamaz. `identity manage` ya da Adapters içindeki S; önce/sonra kayıtlarını, karşılaştırmayı, yedeklemeyi, mevcut GUID metinleri ve sürücünün desteklediği MAC ayarlarının gözden geçirilerek değiştirilmesini sunar. DHCP yenileme, DNS önbelleğini temizleme ve sınırlı Roblox temizliği de vardır. Ürün yazılımı kimliklerini değiştirme, bağımsız eşzamanlı bilgisayar ortamları ve yeni genel IP yönlendirmesi bu işlemlerin kapsamında değildir. Yönetici yetkisi isteyen gerçek değişiklikler, gözden çıkarılabilir bir Windows kurulumunda sınanmalıdır. İngilizce [kapsam ve geri yükleme](../system-identity.md) ile [deneyim açma](../desktop-launch.md) kılavuzları.

Bu README Türkçedir. Uygulama arayüzü ve komutlar şimdilik İngilizce kalır.

## Gereksinimler

Derlenmiş uygulama için Windows x64 ve [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0) gerekir. Kaynak koddan çalıştırmak için .NET SDK 10 kullanın. Hesap girişi ayrıca [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) ve internet bağlantısı gerektirir.

## İlk çalıştırma

Projenin kök klasöründe PowerShell açın ve uygulamayı kaynak koddan çalıştırın:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Yerel derleme hazırsa aşağıdaki komutlarla açabilirsiniz. Yardımcı dosyaları myka.exe ile aynı klasörde tutun:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Hesap ekleme

1. Accounts sekmesinde A tuşuna basın. Roblox, kendine ait WebView2 profili olan bir pencerede açılır.
2. Parolanızı ve doğrulama kodlarını yalnızca Roblox sayfasına girin. Giriş yaptıktan sonra Verify account düğmesine tıklayın.
3. Uygulama `https://users.roblox.com/v1/users/authenticated` yanıtını kontrol eder. Adı ve sayısal kimliği kontrol edip Save account düğmesine tıklayın.

Hesaplar sayısal Roblox kimliğiyle ayırt edilir; aynı hesap iki kez kaydedilemez. Yeniden giriş yeni bir profilde yapılır. Farklı bir hesaba giriş yapmak veya işlemi iptal etmek kayıtlı hesabı değiştirmez. `not checked`, oturumun geçerliliğinin o anda kontrol edilmediği anlamına gelir.

## Komutlar

Bu komutları `.\.artifacts\publish\myka.exe` sonuna ekleyin. `LOCAL_ACCOUNT_ID` yerine `accounts list` çıktısındaki yerel kimliği yazın; sayısal Roblox kimliğini kullanmayın. Ekleme, yeniden giriş ve kaldırma için etkileşimli terminal gerekir.

| Komut | İşlev |
| --- | --- |
| `accounts list` | Kayıtlı hesapları ve yerel kimliklerini listeler. |
| `accounts add` | Yeni hesap için giriş penceresini açar. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Seçili hesaba yeniden giriş yapar. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Hesabı ve yerel profilini kaldırmak için onay ister. |
| `accounts cleanup` | Bekleyen profil temizliğini yeniden dener. |
| `doctor` | Windows, .NET, WebView2 ve varsayılan Roblox klasörünü kontrol eder. |
| `identity inspect` | Ağ bağdaştırıcılarını ve Windows'un bildirdiği adresleri gösterir. |
| `logo` | Logoyu gösterir. |
| `--help` | Komut yardımını gösterir. |

## Klavye kontrolleri

| Tuşlar | İşlem |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Sekmeler arasında geçiş yapar. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Hesap seçer veya liste ve raporlarda kaydırır. |
| `A, S / Enter, D / Delete, C` | Accounts içinde: ekle, yeniden giriş yap, kaldır, profilleri temizle. |
| `R` | Hesap listesini yeniden yükler veya raporu yeniler. |
| `F1 / ?, Esc` | Tuş rehberini açar; Esc rehberi kapatır. Rehber veya onay açık değilken Esc ya da Q uygulamadan çıkar. |
| `Space` | Logonun dönüşünü duraklatır veya sürdürür. |
| `Y` | Kaldırmayı onaylar. Diğer tüm tuşlar hesabı korur. |
| `Ctrl+C` | İptal eder ve 130 çıkış koduyla kapanır. |

Sekmeli ekran en az 50 sütun ve 12 satır gerektirir. `--plain`, renk ve animasyon olmadan numaralı menü gösterir; `--still`, logoyu hareketsiz başlatır. Yönlendirilmiş çıktı, `NO_COLOR` ve `TERM=dumb` da düz metin kullanır.

## Verilerin saklanması

Veriler `%LOCALAPPDATA%\MyKa\RobloxSpoofer` altında saklanır. Her hesabın tarayıcı oturum verilerini tutan ayrı bir WebView2 profili vardır. `accounts.json`, parola veya çerez yerine kimlikleri, adları ve doğrulama zamanlarını içerir. Uygulama çerezleri ya da giriş formunun alanlarını okumaz.

Klasöre geçerli Windows kullanıcısı, SYSTEM ve yöneticiler erişebilir. Meta veriler `.bak` yedeğiyle atomik olarak değiştirilir; geçersiz veriler ve bilinmeyen şema sürümleri olduğu gibi bırakılır. Hesap kaldırıldığında yerel profil silinir, ancak diğer cihazlardaki Roblox oturumları sonlandırılmaz.

## Sorun giderme

- `WebView2 unavailable` görürseniz WebView2 Runtime'ı yükleyin veya onarın. Giriş penceresi yalnızca belirlenen Roblox sayfalarına HTTPS üzerinden izin verir. Başka sitelere yönlendiren harici sağlayıcılarla giriş desteklenmez.
- Accounts, `unavailable` gösteriyorsa diğer hesap işlemlerini kapatıp R tuşuna basın. Bozulmuş `accounts.json` dosyasının yerine boş dosya koymayın.
- Temizlik tamamlanmazsa giriş pencerelerini kapatıp `accounts cleanup` çalıştırın. Eksik bir `doctor` kontrolü, Roblox'un kurulu olmadığını kanıtlamaz. Store kurulumları ve standart dışı klasörler algılanmaz.

Son kod doğrulamasında 691 test geçti. Gerçek Roblox penceresinde giriş, iki hesapla denemeler ve gerçek terminallerdeki davranış hâlâ elle kontrol edilmeyi bekliyor. İstemci dosyalarının bulunması, uyumluluğu veya birden fazla istemcinin aynı anda çalışmasını doğrulamaz.

## Diğer belgeler

- [Mimari (İngilizce)](../architecture.md)
- [Araştırma ve sınırlamalar (İngilizce)](../research.md)
- [Terminal arayüzü (İngilizce)](../terminal-ui.md)
- [Derleme ve test bilgilerini içeren tam İngilizce README](../../README.md)
