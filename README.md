# 🏝️ Dynamic Island for Windows (Dinamik Ada)

<p align="center">
  <img src="app.ico" width="80" height="80" alt="Dynamic Island Icon"/>
  <br>
  <b>Windows için Ultra Akıcı, Modern ve Çok Fonksiyonlu Dinamik Ada Deneyimi</b>
  <br>
  <i>macOS NotchNook ve Apple Dynamic Island estetiğinden ilham alan; medya kontrolü, sistem takibi ve oyuncu araçlarını tek bir yüzen arayüzde birleştiren açık kaynaklı masaüstü asistanı.</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10"/>
  <img src="https://img.shields.io/badge/WPF-XAML-blue?style=for-the-badge&logo=windows&logoColor=white" alt="WPF"/>
  <img src="https://img.shields.io/badge/Platform-Windows_10_%2F_11-0078D6?style=for-the-badge&logo=windows11&logoColor=white" alt="Windows"/>
  <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="License MIT"/>
</p>

---

## 🌟 Öne Çıkan Özellikler

### 1. 🎵 Apple Music & NotchNook Tarzı Medya Oynatıcı
- **Evrensel Entegrasyon:** Spotify, Chrome, Edge, Brave ve Windows Sistem Medyası (SMTC) ile anlık canlı senkronizasyon.
- **Şık Arayüz:** Yuvarlatılmış albüm kapağı, parça adı, sanatçı, canlı oynatma/duraklatma durumu ve zaman çubuğu (`0:00` / `3:45`).
- **Sağ Üst Rozet:** Çalan platforma göre dinamik renk değiştiren rozet (Spotify Yeşil, Brave Turuncu, Chrome Mavi).
- **Kompakt Hap Modu:** Müzik çalarken adanın üstünde mini dairesel kontroller ve canlı ritim tutan equalizer animasyonu.

### 2. 📥 AirDrop Tarzı Dosya Rafı (Tepsi / File Shelf)
- Masaüstünden veya herhangi bir klasörden dosyaları tutup adaya sürükleyip bırakın.
- Dosyalar adada geçici olarak depolanır; istediğiniz an tutup Discord'a, WhatsApp'a veya başka bir klasöre geri sürükleyebilirsiniz.
- Kalıcı liste desteği (`shelf.json`) ile uygulama kapansa bile dosyalarınız rafta durur.

### 3. 🚀 macOS Tarzı Kısayol Çubuğu (Quick Apps Dock)
- En sık kullanılan uygulamaları (Chrome, Spotify, Steam, Discord, VS Code) tek tıkla açın.
- **Özel Program Ekleme:** Bilgisayarınızdaki herhangi bir `.exe` dosyasını seçerek kendi kısayolunuzu oluşturabilirsiniz (`quickapps.json`).

### 4. 💻 Canlı Donanım & Sistem Monitörü
- **İşlemci (CPU):** Gerçek zamanlı kullanım yüzdesi ve durum barı (Win32 `GetSystemTimes` sıfır gecikme).
- **Bellek (RAM):** Toplam ve kullanılan RAM miktarı (`GlobalMemoryStatusEx`).
- **Güç & Tarih:** Pil durumu, priz bağlantısı ve Türkçe gün/tarih bilgisi.
- Görev Yöneticisi'ne tek tıkla hızlı erişim.

### 5. 🎮 Oyuncu Araçları (Gaming Hub)
*Ekranda yer kaplamayan, şık alt sekmelerle organize edilmiş güçlü oyuncu modülleri:*

- 💣 **CS2 Bomba & Çözme Asistanı (C4 Timer):**
  - Valve Resmi **Game State Integration (GSI)** protokolü ile çalışır.
  - Oyun belleğine **asla dokunmaz**, harici port dinler (%100 VAC güvenlidir).
  - C4 patlama süresi, kitsiz çözme payı (10.0s) ve kitli çözme payı (5.0s) anlık hesaplanır.
  - 10. saniyede ve 5. saniyede sesli uyarı bip sesi verir.
- ⚔️ **Minecraft AFK Mob Farm Asistanı:**
  - Sadece Minecraft penceresi odaktayken çalışır (Alt+Tab yapınca otomatik durur).
  - Belirlenen aralıklarla canavarları keser (2x sol tık) ve sol eldeki yemeği otomatik yer.
- 🎯 **Gamer Crosshair (Özel Nişangah):**
  - %100 Click-Through (tıklama geçiren) şeffaf ekran ortası katmanı.
  - 4 Farklı Stil: Nokta (Dot), Halka (Ring), Artı (Plus), Sniper Glow.
  - Boyut, renk, opaklık ve hassas X/Y ofset kalibrasyonu.
- ⏱️ **Hassas Kronometre & Süreölçer:**
  - Milisaniye hassasiyetli tur (lap) kayıtlı kronometre.
  - Oyun bekleme süreleri (cooldown) için +1, +3, +5, +10 dakikalık hızlı hazır süreölçer.

### 6. ✨ Gemini AI & Sesli Asistan
- `Alt + V` kısayolu ile bas-konuş mikrofon kaydı.
- Sesli komutlarla şarkı aratma, ses yükseltme/kısma/sessize alma ve genel sorulara yanıt alma.

### 7. 🌐 Gamer & Günlük Çevirmen (Translator)
- Türkçe ⇄ İngilizce anlık çift yönlü çeviri.
- GG, "Bana silah at", "Teşekkürler", "Yardım et" gibi tek tıkla panoya kopyalanan hızlı kalıplar.

### 8. 🎨 Görünüm & Manyetik 6-Köşe Konumlandırma
- **Temalar:** Apple OLED Siyahı, Spotify Neon, Brave Sunset, Cyberpunk, Sakura Pembe, Cyan Buzul, ve Canlı Kayan RGB Chroma.
- **Anime Maskot & Canlı GIF:** Adanın köşesinde müzikle kafa sallayan sevimli anime karakteri veya kendi `.gif`/`.png` görselinizi arka plan yapma imkanı.
- **Manyetik 6-Köşe:** Ekranın Sol Üst, Üst Orta, Sağ Üst, Sol Alt, Alt Orta, Sağ Alt köşelerine tek tıkla sabitleme veya serbestçe sürükleyip bırakma.

### 9. ⚡ 120 FPS Organik Yay Fiziği (Fluid Spring)
- Genişleme ve daralma animasyonlarında Apple benzeri elastik yay fiziği (`BackEase { Amplitude = 0.18 }`).
- İçerik geçişlerinde pürüzsüz `Fade-In / Fade-Out` (kırpılma veya sıçrama olmaz).

---

## ⌨️ Varsayılan Kısayol Tuşları

| Kısayol | İşlev |
|---|---|
| `Alt + D` | Dinamik Adayı Aç / Kapat (Genişlet / Küçült) |
| `Alt + M` | Fare İmlecini Doğrudan Adaya Işınla (Oyun içi hızlı kontrol) |
| `Alt + V` | Gemini AI Sesli Asistanı Dinlemeye Başlat |
| `Alt + P` | Müziği Oynat / Duraklat |
| `Alt + K` | Minecraft AFK Farmını Başlat / Durdur |
| `Alt + X` | Gamer Crosshair Aç / Kapat |

*(Tüm kısayollar Ayarlar sekmesinden dilediğiniz gibi değiştirilip kaydedilebilir.)*

---

## 🛠️ Kaynak Koddan Derleme (Build)

Projeyi kendi bilgisayarınızda kaynak koddan derlemek için:

### Gereksinimler
- Windows 10 (Sürüm 19041+) veya Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 1. Projeyi Klonlayın
```bash
git clone https://github.com/KULLANICI_ADINIZ/DynamicIsland-Windows.git
cd DynamicIsland-Windows
```

### 2. Projeyi Çalıştırın
```bash
dotnet run
```

### 3. Tek Parça Bağımsız (.exe) Olarak Paketleme (Publish)
Hiçbir ek kurulum gerektirmeyen taşınabilir tek bir `.exe` üretmek için:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./Publish"
```

---

## 📁 Proje Mimarisi

```
DynamicIsland/
├── Models/
│   └── MediaInfo.cs               # Şarkı, sanatçı ve SMTC durum modeli
├── Services/
│   ├── MediaService.cs            # Windows SMTC & medya yöneticisi
│   ├── FileShelfService.cs        # Sürükle-bırak dosya rafı & ikon çıkarıcı
│   ├── QuickAppsService.cs        # Kısayol dock & .exe yöneticisi
│   ├── SystemService.cs           # Win32 CPU, RAM ve Güç servisi
│   ├── Cs2GsiService.cs           # Valve resmi HTTP GSI sunucusu (Port 13371)
│   ├── MinecraftAfkService.cs     # Windows API odak ve fare tıklama servisi
│   ├── HotKeyService.cs           # Global düşük seviye kısayol kancası
│   ├── GeminiService.cs           # Google Gemini AI API istemcisi
│   ├── AudioRecorderService.cs    # Mikrofon ses kaydı servisi
│   ├── TranslateService.cs        # Çift yönlü çeviri motoru
│   ├── VolumeService.cs           # Sistem ses kontrol servisi
│   └── GifPlayerService.cs        # Canlı GIF & anime animasyon çözücü
├── App.xaml / App.xaml.cs         # Tek örnek (single-instance) kontrolü & başlangıç
├── MainWindow.xaml / .cs          # Dinamik Ada ana arayüzü, sekmeler ve animasyonlar
├── CrosshairWindow.xaml / .cs     # 100% şeffaf ve tıklama geçiren nişangah katmanı
├── DynamicIsland.csproj           # .NET 10 Proje yapılandırması
├── app.ico                        # Özel Dinamik Ada simgesi
├── LICENSE                        # MIT Lisansı
└── README.md                      # Proje dökümantasyonu
```

---

## 📄 Lisans
Bu proje [MIT Lisansı](LICENSE) kapsamında açık kaynak olarak yayınlanmıştır. Dilediğiniz gibi geliştirebilir, değiştirebilir ve kullanabilirsiniz.
