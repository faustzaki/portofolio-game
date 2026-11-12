# Rencana: Sistem Interaksi Kosan + Menu Portfolio 3D

**File:** `.hermes/plans/2026-11-09_202139-kosan-interaksi-portfolio-3d.md`
**Dibuat:** 2026-11-09 · **Direvisi:** 2026-11-12 (setelah upgrade Unity 6 + keputusan user)
**Status:** Fase A belum mulai. Semua keputusan sudah dijawab (lihat §2).

---

## Goal

Kamar kosan `portofolio-game` jadi *shell* portfolio: player jalan, dekat kasur/PC/TV → muncul prompt, tekan tombol → karakter duduk / duduk-main-PC / nonton-TV dengan transisi kamera halus. Tiap "station" membuka panel portfolio:

**kasur = CV · PC = Projects · TV = Portofolio**

---

## 0. Fakta terukur (baseline)

> Semua angka di bawah **diukur ulang di Unity 6 pada 2026-11-12**, bukan warisan catatan lama.

### Stack (Unity 6.6)

| Item | Nilai |
| --- | --- |
| Editor | **6000.6.7f1** (Unity 6.6), changeset `b22767f04f04` |
| Render pipeline | **Built-in** (`GraphicsSettings.defaultRenderPipeline = NONE`; URP 17.6.0 ada di lock, **tidak aktif**) |
| Cinemachine | **6.6.0** (source: builtin) — API baru: `CinemachineBrain` + `CinemachineCamera` |
| Timeline | 6.6.0 |
| Input System | **1.19.0**, `activeInputHandler: 2` (Both) |
| WebGL module | **ADA** (`BuildPipeline.IsBuildTargetSupported` = True) |
| Windows Standalone | ADA (`True`) |
| `collab-proxy` | **DIBUANG** (project pakai git / Visible Meta Files) |

Catatan upgrade: `com.unity.modules.vr` dihapus Unity 6; 684 error lama berasal dari `collab-proxy` 2.8.2 + `inputsystem` 1.14.0 (bukan kode kita). Commit `6631fb5` (pre) + `5653aba` (upgrade).

### Titik interaksi (world space — diukur 2026-11-12)

| Station | Objek | Posisi | Yaw | Permukaan duduk |
| --- | --- | --- | --- | --- |
| **Kasur** | `PP_Bed` | `(-1.550, -0.540, -1.850)` | 0° | **top y = -0.447** (297/400 sel grid) |
| | `SitSpot_Bed` | `(-1.550, -0.336, -1.000)` | 0° | anchor tanpa renderer |
| | `PP_Bed` bounds | y `[-0.951, -0.129]` | | `MattressSitDent` aktif |
| **PC** | `PP_Desk_Chair` | `(1.500, -0.280, -1.500)` | 180° | collider top **0.4724**, bounds max y 0.442 → **ukur ulang di Fase A** |
| | `Meja_Belajar_Baru` | `(1.650, -0.950, -2.450)` | 0° | meja |
| | `Laptop_Belajar` | `(1.050, -0.100, -2.300)` | 29° | laptop |
| | `PP_Desk_Computer Screen` | `(1.675, -0.100, -2.621)` | 181.8° | monitor |
| | `PP_Desk_System_unit.001` | `(2.280, -0.720, -2.426)` | 0° | PC case |
| **TV** | `PP_OldTV` | `(2.304, -0.130, 2.209)` | 44.2° | bounds y `[-0.340, 0.080]` |
| | `Meja_TV_Baru` | `(2.264, -0.950, 2.224)` | 44.6° | meja |
| | `PP_Armchair` | `(0.329, -0.525, 0.218)` | 224.2° | bounds max y **-0.100** → **ukur ulang di Fase A** |

- Kursi PC & armchair saling hadap dengan TV — jarak armchair↔TV **3.15 m**.
- `PP_Armchair` hadap `fwd = (-0.70, 0, -0.72)`.

### Prop yang masih OFF (jangan dibuka topiknya dulu)

`PP_Desk_Desk-V86Go2rlnq` · `Meja_TV_Shelf` · `PP_WindowBlinds` · `Indo_Pintu` · `Indo_Ceiling` · `WindowWhiteFrame` · `Mesh_0` (node di bawah `Room_6x6_Master`)

### Animasi yang ADA sekarang

| Klip | Sumber | Panjang | Loop | Dipakai? |
| --- | --- | --- | --- | --- |
| `Idle` | `Character_Master.fbx` | 2.00 s | ya | ✅ state `Idle` |
| `Walk` | `Character_Walk.fbx` | 1.03 s | ya | ✅ state `Walk` |
| `Armature\|Sit` | `Character_Sit.fbx` | **7.00 s** | **tidak** | ✅ state `Sit` |
| `mixamo.com` | `TestIdle.fbx` | 2.00 s | tidak | ❌ **tidak dipakai controller** |

- `Character_Animator.controller` — param `Speed:Float` + `IsSitting:Bool`, 3 state (`Idle`/`Walk`/`Sit`).
- **BELUM ADA klip:** duduk-main-PC, nonton-TV, berdiri/transisi masuk.
- `Anim_Sit.fbx` / `Anim_PickUp.fbx` / `Anim_Walk.fbx`: importer 1 clip, **0 `AnimationClip` asset** → gerakan grab tidak tersedia.
- Rig `humanDescription` Mixamo, `avatar = Character_MasterAvatar` (human, shared) → **klip Mixamo baru langsung nyantol**.
- `PlayerController.applyRootMotion = false`; klip Sit `humanMotion = true`.

### Kamera & UI

- `Main Camera` — `IsometricCameraFollow` aktif, target `Player_Chibi`, offset `(-2.45, 1.90, 2.45)`, smooth 4, clamp `x/z ±2.80`, `y 1.30–1.80`. Jarak ke pemain 3.33–4.17 m.
- `SitPreviewCam` — depth **-1**, alat verifikasi duduk. **Jangan hapus.**
- **0 komponen Cinemachine** di scene (brain 0, vcam 0).
- Canvas **0**, EventSystem **0**, `PlayerInput` **0**. Belum ada framework UI.
- `PlayerController.cs` pakai legacy `Input.GetAxisRaw` + `Input.GetKeyDown(KeyCode.Tab)` → jalan karena `activeInputHandler: 2`.

### Ukuran aset (untuk keputusan build web)

| Folder / file | Ukuran |
| --- | --- |
| `Assets/Environment` | **57.0 MB** (106 file) — ada room shell duplikat `Indo_Complete_*` (7.2 + 2.3 + 2.3 MB) |
| `Assets/Character` | **20.9 MB** |
| `Assets/Plugins` (MCP/NuGet) | **17.8 MB** — **wajib dikeluarkan** dari build web |
| `PP_Bed_SitDent_swallow.asset` | **20.6 MB** — varian cekungan yang tidak dipakai scene |

Target sehat WebGL: **< 25 MB** → butuh prune (lihat Fase F).

---

## 1. Keputusan yang sudah final

> Semua pertanyaan lama sudah dijawab user 2026-11-12. **Tidak ada lagi gate keputusan di depan.**

| # | Topik | Keputusan |
| --- | --- | --- |
| D1 | Target platform | **WebGL dari Unity 6.6** (module ada di editor ini). Project sudah di-upgrade, jadi tidak ada port terpisah. |
| D2 | Menu: 3D atau overlay? | **Overlay HTML di atas canvas** — teks 3D = masalah scaling/font/aksesibilitas. Fase E merender di Unity dulu (UI Toolkit); jembatan HTML menyusul di Fase F. |
| D3 | Klip duduk PC & TV | **Uji klip `Sit` yang ada dulu** di kursi + armchair. Kalau jelek, baru ambil klip Mixamo. Gate di A4. |
| D4 | Isi tiap station | **kasur = CV · PC = Projects · TV = Portofolio** |
| D5 | Prop OFF | **Biar dulu** — jangan buka topik baru sampai interaksi jadi. |
| D6 | Kontrol | Keyboard WASD (sudah ada) + tombol `E`; mobile = joystick on-screen (belakangan). |
| D7 | Mode tampilan web | **Full screen** — UI web hilang total saat game jalan (gaya Lusion). |
| D8 | Kamera gameplay | **Sudut ruangan follow pemain (CCTV)** = `IsometricCameraFollow` yang sudah ada. Saat interaksi, kamera **cinematic** menyorot karakter sambil panel portfolio terbuka. |
| D9 | Integrasi web | **Ditunda.** Fokus: game sederhana berjalan baik dulu. Web/penempatan panel dipikirkan belakangan. |
| D10 | Scope | **Tidak melanjutkan ini jadi game besar.** Cukup game sederhana yang jalan baik. |

### Yang kuat dari ide asli (pertahankan)

1. Portfolio sebagai game 3D — *differentiator* nyata untuk posisi QA/frontend, dan kamu punya cerita "saya tes sendiri fiturnya".
2. Station-based interaction (kasur/PC/TV) — granularitasnya pas: 1 station = 1 "halaman".
3. Kamera cinematic transisi — ini yang bikin kerasa bukan sekadar demo.

### Yang diubah dari rencana awal (dan alasannya)

1. **Menu tidak dirender sebagai teks 3D.** Yang dikerjakan di 3D: dunia + karakter + kamera + interaksi. Isi menu = panel screen-space. Alasan: teks 3D bermasalah di scaling/font, dan nanti gampang ditukar jadi overlay HTML.
2. **Jangan bikin 3 klip dulu.** Uji `Sit` yang ada di 3 station. Bikin klip baru = kerja mahal yang mungkin tidak perlu.
3. **Halaman awal tetap butuh fallback non-3D.** Kalau WebGL gagal (iGPU / device lama), harus ada tombol masuk yang elegan, bukan layar hitam.
4. **Dokumentasi test = aset portfolio.** Tiap task ninggalin test case + hasil eksekusi (kebiasaan QA). Kumpulkan di `docs/qa/` → deliverable "QA artifacts for my own product".

---

## 2. Arsitektur teknis

```text
Assets/Scripts/Interaction/
  Interactable.cs         // data 1 hotspot: id, prompt, anchor, camera vcam, clip, exit pose
  InteractionSystem.cs    // trigger detect + prompt + FSM + input
  PortfolioMenu.cs        // panel UI Toolkit + event ke station
  PoseAligner.cs          // hitung offset duduk: hips bone → permukaan duduk (raycast, numeric)

Assets/Scripts/Camera/
  CameraRigBindings.cs    // CinemachineBrain + VCam_Follow/Bed/Desk/TV
```

**FSM interaksi:**

```text
Free ──trigger enter + tekan tombol──► Moving   (kamera blend ke vcam station, 1.2 s, EaseInOut)
Moving ──blend selesai──► SitDown               (CrossFade "Sit" 0.35 s + MatchTarget ke anchor)
SitDown ──exit time 1.5 s──► InPose             (panel portfolio buka; player terkunci)
InPose ──ESC/keluar──► Standing                 (CrossFade "Idle" 0.3 s; kamera blend balik)
```

- **1 `Interactable` per hotspot**, `AddComponent` ke GameObject root baru dengan `BoxCollider isTrigger = true` — supaya **tidak ada transform prop yang disentuh**.
- Prompt = panel screen-space kiri-bawah (`[E] Duduk di kasur`) → konsisten dengan panel menu, nol komponen 3D tambahan.

**Kamera (Cinemachine 6.6.0):**

- `CinemachineBrain` di `Main Camera`; `IsometricCameraFollow` **di-disable** saat rig aktif (2 sistem rebutan = kamera lompat). **Jangan hapus** — biar bisa dibalikin.
- `VCam_Follow` = pengganti isometrik, body `Framing Transposer`.
- `VCam_Bed/Desk/TV` = posisi statis hasil framing, prioritas lebih tinggi saat station aktif.
- Blend = `CinemachineBlenderSettings` EaseInOut 1.2 s. **Kenapa Cinemachine, bukan lerp sendiri:** package sudah terpasang, dan "cinematic" = blend kurva + damping, bukan lerp linear.

**Jembatan ke web (menyusul, bukan bagian fase ini):**

```text
UI (HTML) --klik "Projects"--> postMessage({type:'station', id:'desk'}) --> Unity (jslib)
Unity --prompt "[E] Duduk"-->  postMessage({type:'prompt',  id:'bed'})  --> HTML
```

1 file `.jslib` di `Assets/Plugins/WebGL/` + guard `#if UNITY_WEBGL && !UNITY_EDITOR`.

---

## 3. Rencana langkah (task kecil, tiap task ada bukti)

> Konvensi bukti: tiap task selesai → log angka ke `C:/Belajar Coding/Projects/tools/` (atau `C:\tmp\`) + 1 render, lalu tambahkan barisnya ke `docs/qa/interaction_testcases.md`.

### FASE A — Uji kelayakan (SEBELUM nulis sistem) — 4 task · GATE

- [ ] **A1. Uji klip `Sit` di kursi PC.** Pasang player pakai offset, `CrossFade("Sit")`, render 4 sudut. Ukur: hips.y vs permukaan duduk, kaki nembus lantai? (raycast ke bawah, hitung penetrasi). → `sitfit_desk.txt` + render.
- [ ] **A2. Uji klip `Sit` di armchair + kasur.** Kasur: jalankan `SitFinalVerification.Check()` → `contact_check.txt` harus **PASS** (gap rata 3–6 mm, 0 penetrating).
- [ ] **A3. Ukur bentuk kurva klip `Sit`.** Sample `RootT.y`, `RootQ`, 3 muscle tulang kunci tiap 0.25 s. Jawab: (i) 0–2 s = turun ke duduk atau langsung duduk? (ii) 5–7 s statis? → nentuin titik masuk/keluar transisi. → `sitshape.txt`.
- [ ] **A4. KEPUTUSAN D3** dari hasil A1–A3: pakai klip lama atau ambil klip Mixamo baru. **Gate — jangan lanjut sebelum ini diputuskan.**

### FASE B — Trigger + prompt (tanpa kamera, tanpa menu) — 4 task

- [ ] **B1.** `Interactable.cs` + GameObject `Hotspot_Bed` (BoxCollider trigger, di dalam area kasur, **tanpa mengubah prop**).
- [ ] **B2.** `InteractionSystem.cs`: `OnTriggerEnter/Exit` → state `Prompt`; log `id` + jarak. Bukti: masuk-keluar 3× → log konsisten.
- [ ] **B3.** UI Toolkit: `UIDocument` + panel prompt kiri-bawah. Bukti: render overlay.
- [ ] **B4.** Tombol `E` (`Input.GetKeyDown`, konsisten dengan `PlayerController`) → event `OnInteract(id)`. Bukti: log event.

### FASE C — Kamera cinematic — 5 task

- [ ] **C1.** `CinemachineBrain` di `Main Camera`; disable `IsometricCameraFollow` (catat di log). Bukti: log sebelum/sesudah.
- [ ] **C2.** `VCam_Follow` (Framing Transposer, ikut player) — setel sampai framing ≈ kamera sekarang. Bukti: render A/B.
- [ ] **C3.** `VCam_Bed`, `VCam_Desk`, `VCam_TV` — framing dari probe (`measure_framing.txt`: target di tengah frame ±5 %).
- [ ] **C4.** Blend EaseInOut 1.2 s + prioritas per station. Bukti: 1 video transisi (maks 1 video).
- [ ] **C5.** Blend balik ke `VCam_Follow` saat keluar. Bukti: log `Blend` + render 3 frame (awal/tengah/akhir).

### FASE D — Duduk + animasi — 5 task

- [ ] **D1.** `PoseAligner.cs`: offset dari **hips bone** ke permukaan duduk (raycast), bukan hardcode. Bukti: log posisi sebelum/sesudah.
- [ ] **D2.** State `SitDown`: `SetBool("IsSitting", true)` + `CrossFade("Sit", 0.35f)` + `MatchTarget` ke anchor. Bukti: `contact_check.txt` PASS di kasur.
- [ ] **D3.** Kunci gerak: `CharacterController.enabled = false` selama `InPose`. Bukti: tekan WASD → log posisi 3 s, Δ = 0.
- [ ] **D4.** Keluar: `CrossFade("Idle", 0.3f)` + berdiri di `exitPose` (di luar collider). Bukti: 5× masuk-keluar berturut, posisi akhir == target.
- [ ] **D5.** Station TV + PC: anchor sama, offset beda. Kalau butuh klip baru → **balik ke gate A4**, jangan improvisasi.

### FASE E — Panel portfolio — 4 task

- [ ] **E1.** Skema data: `ScriptableObject PortfolioStation` (id, judul, isi, kind) — **satu sumber data**, jangan dua.
- [ ] **E2.** Panel UI Toolkit: layout 3 station. Isi placeholder dulu.
- [ ] **E3.** Isi nyata: CV (bio, skill), Projects (daftar project), Portofolio (link). Jumlah item panel == jumlah sumber.
- [ ] **E4.** Aksi: link GitHub/LinkedIn + tombol "Download CV" (PDF).

### FASE F — Build web — 5 task

- [ ] **F1.** Build profile: exclude `Assets/Plugins/NuGet/**` (17.8 MB), `Assets/Editor/**`, semua script capture (`*Capture*.cs`, `*Probe*.cs`), dan `SitPreviewCam` dari scene build.
- [ ] **F2.** Prune aset: buang `PP_Bed_SitDent_swallow` (20.6 MB) + room shell duplikat. Target build **< 25 MB**. Bukti: ukuran build.
- [ ] **F3.** Build WebGL + full-screen canvas. Bukti: build jalan, console browser bersih.
- [ ] **F4.** Halaman pembungkus: tombol "Masuk ke Kamar" → canvas. Fallback kalau WebGL gagal. Bukti: Lighthouse + console.
- [ ] **F5.** Mobile: kalau < 768 px, tombol game diganti tampilan statis. Bukti: viewport 375 px.

### FASE G — Polish + dokumentasi QA — 4 task

- [ ] **G1.** SFX/ambience (opsional, file kecil).
- [ ] **G2.** Loading screen (progress dari WebGL downloader).
- [ ] **G3.** Test matrix + test case → `docs/qa/` (ini aset QA-mu).
- [ ] **G4.** 1 video demo 30–45 s + screenshot untuk README/CV.

---

## 4. Verifikasi (angka, bukan "kelihatannya jalan")

| Yang diuji | Metode | Ambang lulus |
| --- | --- | --- |
| Karakter benar-benar duduk | `SitFinalVerification.Check()` (40 sampel raycast) | gap rata **0–8 mm**, max ≤ 15 mm, **0 penetrating** |
| Tidak tembus kursi/kasur | audit vertex body y < -0.40 vs permukaan | 0 penetrating |
| Kamera framing station | `WorldToViewportPoint` anchor | target di **0.35–0.65** viewport x/y |
| Transisi kamera halus | sampel jarak antar-frame posisi kamera saat blend | tidak ada lompatan > 0.5 m/frame |
| Gerak terkunci saat `InPose` | log posisi player 3 s, WASD ditekan | Δ posisi = 0.000 |
| Keluar bersih | 5× masuk-keluar | posisi akhir == target ± 2 mm, tidak nyangkut |
| Trigger akurat | masuk/keluar area 3× | 6 event, id benar, 0 false-positive |
| Panel sinkron | jumlah item panel == jumlah item sumber | sama |
| Ukuran build web | ukuran folder build | < 25 MB |
| Prompt muncul | render + log event | prompt untuk 3 station |

**Ujian manusia (1 video):** jalan → dekat kasur → prompt → tekan E → kamera blend → duduk → panel CV → keluar → ke PC → duduk-main → panel Projects → ke TV → panel Portofolio.

---

## 5. Risiko & mitigasi

| Risiko | Dampak | Mitigasi |
| --- | --- | --- |
| 2 sistem kamera rebutan (`IsometricCameraFollow` + Cinemachine) | kamera lompat-lompat | C1: disable follow, 1 pemilik saja |
| Klip `Sit` 7 s = transisi, bukan loop | duduk jadi "gerak terus" / membeku aneh di akhir | A3: ukur kurva; `MatchTarget` + clamp `normalizedTime` di pose akhir |
| Klip PC/TV tidak ada | kedengeran "belum jadi" | Gate A4: uji klip lama dulu, ambil Mixamo cuma kalau perlu |
| `PP_Bed` punya `MeshCollider` + `MattressSitDent` | karakter nyangkut / cekungan aneh | `contact_check.txt` sebagai acceptance; **jangan hapus** `MattressSitDent` (itu fitur) |
| Platform kursi/armchair belum terukur bersih (collider box kasar) | offset duduk salah | A1/A2 ukur ulang via mesh/`PoseAligner`, jangan pakai angka §0 mentah |
| Aset 57 MB di `Environment` | build web > 50 MB | F2: prune yang tidak dipakai scene |
| Mengubah transform prop saat naruh hotspot | melanggar kunci penempatan user | hotspot = root scene terpisah; sebelum-sesudah dicatat sebagai `UNCHANGED` |
| Font/panel gagal di WebGL | teks tidak kebawa | F4: fallback non-3D |
| **Ubah `Packages/manifest.json` saat Unity jalan** | resolver **tidak** jalan ulang → paket tetap versi lama, `Assembly-CSharp` tidak compile | Restart editor setelah ubah manifest (terbukti saat upgrade Unity 6) |

---

## 6. Batasan (jangan dilanggar)

- **Transform prop tidak disentuh** — hotspot/anchor = objek root baru; offset duduk dihitung, bukan mindahin barang. Kalau perlu geser, minta approval dulu.
- **`SitPreviewCam` dan tool capture jangan dihapus** — itu alat verifikasi.
- **Jangan bikin tool/slider/panel yang tidak diminta user.**
- **1 video per delivery.**
- Semua klaim visual dibuktikan **angka + log + render** dulu sebelum kirim.
- Jangan sentuh `Assets/Plugins/**` atau `Packages/manifest.json` tanpa persetujuan eksplisit.
- **Tidak melanjutkan ini jadi game besar.** Game sederhana jalan baik dulu; penempatan & letak panel dipikirkan belakangan.

---

## 7. Yang sudah selesai sebelum fase ini

| Item | Status |
| --- | --- |
| Upgrade Unity 2022.3.61f1 → **6.6 (6000.6.7f1)** | ✅ commit `5653aba` |
| Snapshot pre-upgrade | ✅ commit `6631fb5` (148 file) |
| Scene `KosanGameScene` setelah upgrade | ✅ 22 root, **0 diff**, 0 material rusak, 50 renderer sehat |
| Kamera gameplay CCTV (`IsometricCameraFollow`) | ✅ aktif, play test **16/16** sampel player on-screen |
| Play test Unity 6 | ✅ `camInRoom True`, jarak 3.33–4.17 m |
| Backup pre-upgrade | ✅ `pre_unity6_20261112/project_meta.tar` (101 MB) |
| Ubin Indonesia + dinding abu + 6 task material | ✅ selesai 2026-11-09 |
