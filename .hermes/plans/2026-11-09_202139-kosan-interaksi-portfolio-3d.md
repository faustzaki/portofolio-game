# Rencana: Sistem Interaksi Kosan + Menu Portfolio 3D

**Goal:** Kamar kosan `portofolio-game` jadi *shell* portfolio — player jalan, dekat kasur/PC/TV → muncul prompt, tekan tombol → karakter duduk/duduk-main-PC/nonton-TV dengan transisi kamera halus, dan tiap "station" ngebuka panel portfolio (CV / Experience / Projects) yang isinya sinkron dengan web.

**Arsitektur singkat:** 3 skrip kecil (`Interactable`, `InteractionSystem`, `PortfolioMenu`) + Cinemachine (3 Virtual Camera: Follow / Focus / Back) + Animator parameter yang sudah ada (`IsSitting`). Titik interaksi = trigger collider, bukan cek jarak di `Update`. Event ke UI via `UnityEvent` (UI Toolkit runtime) → **tidak ada framework baru**, React/Next di-load hanya kalau nanti pindah ke WebGL.

**Stack terukur:** Unity 2022.3.61f1 · Built-in RP (URP 14.0.12 ada di lock tapi **tidak aktif**; `GraphicsSettings.defaultRenderPipeline = NONE`) · Cinemachine **3.1.6** (terpasang, belum dipakai) · Timeline 1.8.12 · InputSystem 1.14.0 + `activeInputHandler: 2` (Both) · WebGL module **TIDAK ADA** di editor ini.

---

## 0. Fakta terukur (baseline — jangan diarang)

### Titik interaksi (world space)

| Station | Anchor | Permukaan / catatan |
| --- | --- | --- |
| Kasur | `PP_Bed` pos `(-1.550,-0.540,-1.850)` | kasur atas y **-0.431 … -0.447**; ada `SitSpot_Bed` `(-1.550,-0.336,-1.000)`, `MattressSitDent` (cekungan PhysX), `contact_check.txt` = acceptance sit |
| PC | `PP_Desk_Chair` `(1.500,-0.280,-1.500)` rot 180° | kursi bounds `0.745×1.392×0.741`; monitor `(1.663,0.127,-2.620)`; keyboard `(1.814,-0.083,-2.240)`; mouse `(1.327,-0.078,-2.199)` |
| TV | `PP_OldTV` `(2.304,-0.130,2.209)` rot 44.2° | meja `Meja_TV_Baru` `(2.264,-0.950,2.224)` rot 44.6°; `PP_Armchair` `(0.329,-0.525,0.218)` rot 224.2° (yaw sama+180 → saling hadap, jarak **3.15 m**) |

### Animasi yang ADA sekarang

| Klip | Sumber | Panjang | Loop |
| --- | --- | --- | --- |
| `Idle` | `Character_Master.fbx` | 2.00 s | ya |
| `Walk` | `Character_Walk.fbx` | 1.03 s | ya |
| `Armature\|Sit` | `Character_Sit.fbx` | **7.00 s** | **tidak** |

- `Character_Animator.controller` = 3 state (`Idle`, `Walk`, `Sit`), parameter `Speed` (float) + `IsSitting` (bool).
- **BELUM ADA klip:** duduk-main-PC, nonton-TV, berdiri + transisi masuk.
- `Anim_Sit.fbx` / `Anim_PickUp.fbx` / `Anim_Walk.fbx`: importer punya 1 clip, **tapi 0 `AnimationClip` asset** (tidak ke-import / di-preserve). Jadi gerakan grab tidak tersedia.
- Semua rig `humanDescription` Mixamo, `avatar = Character_MasterAvatar` (human, shared) → klip Mixamo baru langsung nyantol.
- `PlayerController.applyRootMotion = false`; klip Sit `humanMotion = true`, kurva = `RootT.* / RootQ.* / FootT.* / FootQ.*` + muscle humanoid (rotasi tulang, jadi ikut avatar).

### Kamera

- `Main Camera` — punya `IsometricCameraFollow` (aktif, target `Player_Chibi`, offset `(-2.45,1.90,2.45)`, clamp `x/z ±2.80`, `y 1.30–1.80`).
- `SitPreviewCam` — depth **-1**, dipakai capture duduk (jangan dihapus; ini tool verifikasi user).
- **0 komponen Cinemachine** di scene.

### UI / Input

- Canvas **0**, EventSystem **0**, `PlayerInput` **0**. Belum ada framework UI sama sekali.
- `PlayerController.cs` pakai legacy `Input.GetAxisRaw` + `Input.GetKeyDown(KeyCode.Tab)` → jalan karena `activeInputHandler: 2`.

### Kunci yang sudah disepakati user

- **Penempatan objek DIKUNCI** — transform prop tidak boleh diubah. Titik duduk wajib dirender/offset tanpa mindahin prop. (Kalau perlu geser sedikit, minta approval dulu.)
- Verifikasi = **angka + log + render**, bukan cuma vision.
- Video maks 1 per delivery.
- Atur dari Unity (bukan hack di script render).

### Jejak jejak portfolio (hasil recon)

| Repo | Stack | Isi |
| --- | --- | --- |
| `C:\Belajar Coding\Projects\portofolio` | Vite + React | **sudah ada** `SLUGPORTFOLIO_PLAN.md` (redesign: Navbar/Hero/About/Skills/Projects/Contact) |
| `C:\Belajar Coding\Projects\Hybrid_Showcase_Portfolio` | Next 16 + React 19 + Tailwind 4 + Framer Motion + Lenis | **dual-mode**: Recruiter (DOM murni, default) + Showcase (3D Lusion-style via `React.lazy`, desktop-only). Data tunggal `src/data/seed.ts` (`Project[]`, `bio`, `SkillGroup[]`, `Experience[]`, `SocialLink[]`). 4 commit, jalan. |

→ Isi CV/Experience/Projects **sudah ada** di `seed.ts`. Menu 3D cukup **baca data yang sama**, jangan bikin skema baru. Aturan AGENTS.md §4: label mode pakai `VIEW PROJECT` / `EXPLORE`, palet `#0A0A0C / #121518 / #22262B / #5E6AD2 / #00F0FF`, font Cabinet Grotesk + Satoshi.

### Ukuran aset (untuk keputusan build web)

| Folder | Ukuran |
| --- | --- |
| `Assets/Environment` | **57.0 MB** (106 file) — 3 room shell duplikat (`Indo_Complete_*` 7.2 + 2.3 + 2.3 MB) |
| `Assets/Character` | **20.9 MB** |
| `Assets/Plugins` (MCP/NuGet) | **17.8 MB** — **wajib dikeluarkan** dari build web |
| file terberat | `PP_Bed_SitDent_swallow.asset` **20.6 MB** (varian cekungan yang tidak dipakai scene) |

Target sehat WebGL: **< 25 MB** → butuh prune (lihat §6).

---

## 1. Jawaban atas ide — yang kuat, yang perlu diubah

**Yang kuat (pertahankan):**
1. Portfolio sebagai game 3D — *differentiator* nyata untuk posisi QA/frontend, apalagi kamu punya cerita "saya tes sendiri fiturnya".
2. Station-based interaction (kasur/PC/TV) — granularitasnya pas: tiap station = 1 "halaman".
3. Kamera cinematic transisi — ini yang bikin kerasa "bukan sekadar demo".

**Yang perlu diubah (dan alasannya):**
1. **Jangan tunda keputusan build web.** WebGL module tidak ada di Unity 2022.3.61f1 di mesin ini (terukur: cuma `windowsstandalonesupport`; WebGL ada di 6000.6.7f1). Ini memutus antara "1 repo, 1 URL" vs "2 app (game + web)". Efeknya besar ke arsitektur menu → **putuskan di §2.**
2. **Jangan bikin menu 3D penuh di dalam game.** Yang dikerjakan di 3D: *dunia + karakter + kamera + interaksi*. Isi menu (daftar CV, pengalaman) = **HTML di atas game** (overlay) — bukan TextMeshPro 3D. Alasan: teks 3D = masalah scaling/font/aksesibilitas, dan kamu sudah punya sistem tipografi bagus di web.
3. **Satu klip Sit untuk 3 station = risiko kelihatan aneh** (kaki nembus kursi, tangan tidak di keyboard). Urutan yang murah: (a) uji dulu klip Sit yang ada di kursi PC & armchair dengan alignment offset + `MatchTarget`; (b) kalau jelek, baru ambil klip Mixamo per-station. **Jangan langsung bikin 3 klip** sebelum tahu klip lama cukup atau tidak.
4. **Halaman awal harus punya fallback DOM.** Aturan AGENTS.md §2 sudah melarang 3D di bundle awal. Jadi: halaman awal = hero DOM + tombol **"Masuk ke Kamar"** → baru load game. Ini sekaligus jaring kalau WebGL gagal (device lama / iGPU).
5. **Dokumentasi test = aset portfolio.** Tiap task di bawah wajib ninggalin test case + hasil eksekusi (persis kebiasaan QA). Kumpulkan di 1 folder → jadi deliverable "QA artifacts for my own product".

---

## 2. Keputusan yang HARUS diputuskan sebelum koding

| # | Pertanyaan | Opsi | Rekomendasi |
| --- | --- | --- | --- |
| D1 | Target platform game | (a) WebGL dibuild dari **Unity 6000.6.7f1** (WebGL module sudah ada) — 1 repo kamu yang sekarang (2022.3) tetap jadi tempat kerja, port terpisah (b) install WebGL module 2022.3.61f1 via Unity Hub (butuh download ~1 GB) (c) build desktop only + video/mockup di web | **(a)** — nol biaya, nol port project. Risiko: perlu tes scene buka di 6000 (Upgrade 2022→6000 biasanya auto, tapi plugin MCP Ivan Murzak harus di-*exclude* dari build) |
| D2 | Menu: di dalam 3D atau overlay HTML? | (a) overlay HTML di atas canvas, hover/mesej via `SendMessage` ↔ `postMessage` (b) teks 3D di dalam ruangan (c) kalau desktop-only: panel UI Toolkit Unity | **(a)** |
| D3 | Klip duduk-main-PC & nonton-TV | (a) pakai klip Sit + alignment offset (uji dulu) (b) tambah klip Mixamo "Sitting typing" + "Sitting watching" | **(a) dulu**, keputusan (b) setelah render bukti |
| D4 | Yang dibuka tiap station | kasur→CV · PC→Projects (+ terminal interaktif) · TV→Experience (reel blog/video) | **setuju dengan usulanmu**, tapi urutan: PC→Projects karena itu yang paling kuat dilihat recruiter |
| D5 | Prop yang masih OFF: `Meja_TV_Shelf` (aktif=False), `PP_Desk_Desk-V86Go2rlnq` (aktif=False), `PP_WindowBlinds`, `Indo_Pintu`, `Indo_Ceiling`, `WindowWhiteFrame`, `Mesh_0` | nyalain balik sebagian / biarin | **biarin dulu** — jangan buka topik baru sampai interaksi jadi |
| D6 | Kontrol | keyboard WASD (yang sudah ada) + tombol E/klik; mobile = joystick on-screen | desktop dulu, mobile belakangan |

---

## 3. Arsitektur teknis

```
Assets/Scripts/Interaction/
  Interactable.cs         // data 1 hotspot: id, prompt, anchor, camera vcam, clip, exit pose
  InteractionSystem.cs    // trigger detect + prompt + FSM (Free→Moving→InPose→Exiting) + input
  PortfolioMenu.cs        // UI Toolkit overlay + UnityEvent<string stationId> ke WebGL (kalau ada)
  PoseAligner.cs          // hitung offset duduk: hips bone → seat surface (numeric, bukan nebak)

Assets/Scripts/Camera/  (pakai Cinemachine, jangan lerp manual)
  CameraRigBindings.cs    // 3 CinemachineCamera: VCam_Follow, VCam_Bed, VCam_Desk, VCam_TV
```

**FSM interaksi (`InteractionSystem`):**
```
Free ──trigger enter + tekan tombol──► Moving (kamera blend ke vcam station, 1.2 s, EaseInOut)
Moving ──selesai blend──► SitDown (Animator.CrossFade "Sit", 0.35 s + MatchTarget ke anchor)
SitDown ──exit time 1.5 s──► InPose (menu panel buka; player terkunci)
InPose ──ESC/keluar──► Standing (CrossFade "Idle" 0.3 s, activeControl Free; kamera blend balik)
```
- **1 script `Interactable` di-`AddComponent` ke trigger GameObject** (root scene terpisah, collider `isTrigger = true`) — supaya **tidak ada transform prop yang disentuh**.
- Prompt = UI Toolkit world-space? **Tidak.** Prompt = panel UI Toolkit screen-space kiri-bawah (`[E] Duduk di kasur`) → konsisten dengan overlay menu, nol komponen 3D tambahan.

**Kamera (Cinemachine 3.1.6):**
- `CinemachineBrain` di `Main Camera`; **`IsometricCameraFollow` di-disable** saat brain aktif (2 sistem kamera rebutan = bug).
- `VCam_Follow` = pengganti isometrik (ikuti player) → bodynya `Framing Transposer`.
- `VCam_Bed/Desk/TV` = posisi statis hasil framing, prioritas lebih tinggi saat station aktif.
- Transisi = `CinemachineBlenderSettings` default `EaseInOut 1.2 s`. **Kenapa Cinemachine, bukan lerp sendiri:** package sudah ada (3.1.6), dan "cinematic" itu = blend kurva + damping, bukan lerp linear.

**Sinkron data ke web (kalau D1 = WebGL):**
```
UI overlay (HTML) --klik "Projects"--> postMessage({type:'station', id:'desk'}) --> Unity (jslib)
Unity --prompt "[E] Duduk"--> postMessage({type:'prompt', id:'bed'}) --> HTML
```
Jembatan = 1 file `.jslib` di `Assets/Plugins/WebGL/` + guard `#if UNITY_WEBGL && !UNITY_EDITOR`.

---

## 4. Rencana langkah (task kecil, tiap task ada bukti)

> Konvensi bukti: setiap task selesai → tulis log angka ke `C:/Belajar Coding/Projects/tools/` + 1 render, dan tambahkan barisnya ke `docs/qa/interaction_testcases.md`.

### FASE A — Uji kelayakan (SEBELUM nulis sistem) — 4 task
- [ ] **A1. Uji klip Sit di kursi PC.** Pasang player di kursi pakai offset, `CrossFade("Sit")`, render 4 sudut. Ukur: hips.y vs seat surface, kaki nembus lantai? (raycast ke bawah, hitung penetrasi). Output: `sitfit_desk.txt` + render.
- [ ] **A2. Uji klip Sit di armchair + kasur.** Idem. Kasur: jalankan `SitFinalVerification.Check()` (tool lama) → `contact_check.txt` harus **PASS** (gap rata 3–6 mm, 0 penetrating).
- [ ] **A3. Ukur bentuk kurva klip Sit.** Sample `RootT.y`, `RootQ`, dan 3 muscle tulang kunci tiap 0.25 s; jawab: (i) 0–2 s itu "turun ke duduk" atau langsung duduk? (ii) 5–7 s statis? → nentuin titik masuk/keluar transisi. Output: `sitshape.txt` + grafik.
- [ ] **A4. Keputusan D3** berdasarkan A1–A3: pakai klip lama / ambil klip Mixamo baru. (Gate — jangan lanjut sebelum ada keputusan.)

### FASE B — Trigger + prompt (tanpa kamera, tanpa menu) — 4 task
- [ ] **B1.** `Interactable.cs` + GameObject `Hotspot_Bed` (BoxCollider trigger, isi di dalam area kasur, **tanpa mengubah prop**).
- [ ] **B2.** `InteractionSystem.cs`: deteksi `OnTriggerEnter/Exit` → state `Prompt`; log `id + jarak`. Bukti: masuk-keluar area 3× → log konsisten.
- [ ] **B3.** UI Toolkit: `UIDocument` + panel prompt kiri-bawah. Bukti: render overlay.
- [ ] **B4.** Tombol `E` (pakai `Input.GetKeyDown` biar konsisten) → event `OnInteract(id)`. Bukti: log event.

### FASE C — Kamera cinematic — 5 task
- [ ] **C1.** `CinemachineBrain` di `Main Camera`; matikan `IsometricCameraFollow` (catat di log) — **jangan hapus**, biar bisa dibalikin.
- [ ] **C2.** `VCam_Follow` (Framing Transposer, ikut player) — parameternya disetel sampai framing ≈ kamera sekarang. Bukti: render A/B.
- [ ] **C3.** `VCam_Bed`, `VCam_Desk`, `VCam_TV` — framing dari probe posisi (`measure_framing.txt`: target di tengah frame ±5 %, objek penting tidak terpotong).
- [ ] **C4.** Blend `EaseInOut 1.2 s` + prioritas per station. Bukti: rekam 1 video transisi (maks 1 video).
- [ ] **C5.** Blend balik ke `VCam_Follow` saat keluar. Bukti: log `Blend` + render 3 frame (awal/tengah/akhir).

### FASE D — Duduk + animasi — 5 task
- [ ] **D1.** `PoseAligner.cs`: hitung offset dari **hips bone** ke permukaan duduk (raycast), bukan angka hardcode. Bukti: log posisi sebelum/sesudah, hips vs seat surface.
- [ ] **D2.** State `SitDown`: `Animator.SetBool("IsSitting", true)` + `CrossFade("Sit", 0.35f)` + `MatchTarget` ke anchor. Bukti: `contact_check.txt` PASS di kasur.
- [ ] **D3.** Kunci gerak: `CharacterController.enabled = false` selama InPose (cara paling andal, bukan flag di PlayerController). Bukti: coba tekan WASD → posisi tidak berubah (log posisi 3 detik).
- [ ] **D4.** Keluar: `CrossFade("Idle", 0.3f)` + posisi berdiri di titik `exitPose` (di luar collider kursi/kasur) → cek tidak nyangkut (raycast sphere). Bukti: 5× masuk-keluar berturut, posisi akhir = target.
- [ ] **D5.** Station TV + PC: anchor sama, offset berbeda. Kalau perlu klip baru → **balik ke gate A4**, jangan improvisasi.

### FASE E — Menu portfolio (overlay) — 4 task
- [ ] **E1.** Skema data: 1 `ScriptableObject` `PortfolioStation` (id, judul, isi, kind) — atau langsung `seed.ts` kalau D1=(a). Pilih yang **satu sumber data**, jangan dua.
- [ ] **E2.** Panel UI Toolkit: layout 3 station (CV / Projects / Experience). Isi dulu hardcode 3 baris placeholder.
- [ ] **E3.** Isi nyata: tarik dari `seed.ts` (bio, projects, experience). Konsistensi diuji: jumlah item di game == jumlah di web.
- [ ] **E4.** Aksi panel: link GitHub/LinkedIn + tombol "Download CV" (PDF yang sudah ada di repo web).

### FASE F — Build web + integrasi (kalau D1 = WebGL) — 5 task
- [ ] **F1.** Siapkan build profile: exclude `Assets/Plugins/NuGet/**` (17.8 MB), `Assets/Editor/**`, semua capture script (`*Capture*.cs`, `*Probe*.cs`), `SitPreviewCam`, `Gameplay_Recorder` dari scene build.
- [ ] **F2.** Prune aset: buang varian `PP_Bed_SitDent_swallow` (20.6 MB), room shell duplikat yang tak dipakai. Target build **< 25 MB**. Bukti: ukuran build.
- [ ] **F3.** Build WebGL + jslib bridge (2 arah). Bukti: build jalan, prompt & panggilan station tercatat di console browser.
- [ ] **F4.** Integrasi ke `Hybrid_Showcase_Portfolio`: tombol "Masuk ke Kamar" di hero (DOM) → `React.lazy` → canvas. Fallback kalau WebGL gagal → mode Recruiter. Bukti: Lighthouse + console bersih.
- [ ] **F5.** Mobile: kalau <768px → tombol game disembunyikan (aturan AGENTS.md §2). Bukti: viewport 375px.

### FASE G — Polish + dokumentasi portfolio — 4 task
- [ ] **G1.** SFX/ambience (opsional, file kecil).
- [ ] **G2.** Loading screen (progress bar dari WebGL downloader).
- [ ] **G3.** Test matrix + test case → `Hybrid_Showcase_Portfolio/docs/qa/` atau `portofolio/docs/qa/` (ini aset QA-mu).
- [ ] **G4.** 1 video demo 30–45 s + screenshot untuk README/CV.

---

## 5. Verifikasi (cara buktiin, bukan "kelihatannya jalan")

| Yang diuji | Metode (angka, bukan mata) | Ambang lulus |
| --- | --- | --- |
| Karakter benar-benar duduk | `SitFinalVerification.Check()` (40 sampel raycast, mesh underside vs kasur) | gap rata **0–8 mm**, max ≤ 15 mm, **0 penetrating** |
| Tidak tembus kursi/kasur | audit vertex body y < -0.40 vs permukaan | 0 penetrating |
| Kamera framing station | `WorldToViewportPoint` anchor | target di **0.35–0.65** viewport x/y |
| Transisi kamera halus | sampel jarak antar-frame posisi kamera saat blend | tidak ada lompatan > 0.5 m/frame |
| Gerak terkunci saat InPose | log posisi player 3 s dengan WASD ditekan | Δ posisi = 0.000 |
| Keluar bersih | 5× masuk-keluar | posisi akhir == target ± 2 mm, tidak nyangkut |
| Trigger akurat | masuk/keluar area 3× | 6 event, id benar, 0 false-positive |
| Menu sinkron | jumlah item panel == jumlah item `seed.ts` | sama |
| Ukuran build web | ukuran folder build | < 25 MB |
| Prompt muncul | render + log event | prompt untuk 3 station |

**Ujian manusia (1 video):** jalan → dekat kasur → prompt → tekan E → kamera blend → duduk → panel CV → keluar → ke PC → duduk-main → panel Projects → ke TV → panel Experience.

---

## 6. Risiko & mitigasi

| Risiko | Dampak | Mitigasi |
| --- | --- | --- |
| WebGL module nggak ada di 2022.3.61f1 | Portfolio web mentok | D1: build pakai 6000.6.7f1 (module ada). Cek dulu scene kebuka normal di 6000 |
| Plugin MCP (`Assets/Plugins/NuGet`, 17.8 MB) kebawa build | build bengkak / error WebGL | Kecualikan eksplisit di build profile + `asmdef` kalau perlu |
| 2 sistem kamera rebutan (`IsometricCameraFollow` + Cinemachine) | kamera lompat-lompat | C1: disable follow, 1 pemilik saja |
| Klip Sit 7 s = transisi, bukan loop | duduk jadi "gerak terus" / karakter membeku aneh di akhir | A3: ukur bentuk kurva; pakai `MatchTarget` + `normalizedTime` clamp di pose akhir |
| Klip TV/PC nggak ada | kedengeran "belum jadi" | Gate A4: uji klip lama dulu, ambil Mixamo cuma kalau perlu |
| `PP_Bed` punya `MeshCollider` + `MattressSitDent` | karakter bisa nyangkut / cekungan aneh pas duduk | pakai `contact_check.txt` sebagai acceptance; jangan hapus `MattressSitDent` (itu fitur) |
| Aset 57 MB di `Environment` | build web > 50 MB | F2: prune yang nggak dipakai scene |
| Mengubah transform prop saat naruh hotspot | melanggar kunci penempatan user | trigger = root scene terpisah; sebelum-sesudah dicatat di tabel `UNCHANGED` |
| TextMeshPro/UI Toolkit di WebGL | font nggak kebawa | F4: fallback DOM kalau UI gagal |

---

## 7. Pertanyaan terbuka

1. **D1 platform** — WebGL dari Unity 6000.6.7f1, install module untuk 2022.3.61f1, atau desktop-only dulu?
2. **D3 klip** — boleh uji klip Sit yang ada dulu (Fase A) sebelum beli/ambil klip baru?
3. Station TV isinya apa persisnya: experience kerja, atau blog/reel video? (Ini nentuin layout panel)
4. Apakah kamu mau game-nya **full-screen** (web hilang total, gaya Lusion) atau **canvas di dalam halaman** (header/footer web tetap ada)?
5. Kalau ternyata WebGL berat di device recruiter, apakah boleh default ke mode Recruiter dan game cuma tombol opt-in? (Aturan AGENTS.md §2 bilang iya — konfirmasi)

---

## 8. Batasan (jangan dilanggar)

- Transform prop **tidak disentuh** — hotspot/anchor = objek root baru, offset duduk dihitung, bukan mindahin barang.
- `SitPreviewCam` dan tool capture **jangan dihapus** — itu alat verifikasi.
- Jangan bikin tool/slider/panel yang tidak diminta user.
- 1 video per delivery.
- Semua klaim visual dibuktikan dengan angka/log dulu sebelum kirim.
- Jangan sentuh `Assets/Plugins/**`, `Packages/manifest.json` tanpa persetujuan eksplisit.
