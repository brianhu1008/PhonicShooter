# 佩佩喬治樂園

給小朋友的學習遊戲樂園。一打開是 3D 的樂園，點遊樂設施就能進入遊戲：

- **注音摩天輪 — 注音怪獸大作戰**：第一人稱射擊。角色沿著森林小路自動往前走，遇到一群舉著注音牌子的怪獸，打倒正確的那一隻就會念出拼讀，例如「ㄐ → ㄧ → ㄣ → 金 → 金色」。
- **九九乘法戰機**：像「雷電」的直向捲軸射擊。戰機飛過山、河、湖和森林，敵機載著答案；打中正確答案的那台，整批敵機一起爆炸並念出口訣。打錯會被反擊。最後大魔王出現，一直出題，打中正確的砲台才會受傷。
- **卡皮巴拉發音島 — Phonics Island**：在樂園後面的海上 (滑鼠移到畫面上方，鏡頭就會帶過去)。好人卡皮巴拉游到六座小島學英文自然發音，從 [PhonicIsland](https://github.com/brianhu1008/PhonicIsland) 合併進來。

**👉 直接在瀏覽器玩（iPad 用 Safari 也可以）：https://brianhu1008.github.io/PhonicShooter/**

iPad 上可以按 Safari 的「分享 → 加入主畫面」，之後就像 App 一樣全螢幕開啟。

## 玩法

**注音怪獸大作戰**
- 用滑鼠移動準星、按左鍵發射；iPad 直接點怪獸。
- 37 個注音符號全部都有，題庫有 204 個常用字的拼音和例詞。
- 四種玩法：認識注音、聽一聽拼一拼、看字找注音、耳朵大挑戰。
- 簡單 / 困難兩種難度。怪獸會靠近並出拳、踢腳，血條歸零就輸了；答對會補血。
- 電腦版可以在「錄製注音念法」用學校教的念法自己錄每個注音符號。

**九九乘法戰機**
- 電腦用鍵盤方向鍵 (或 WASD) 移動戰機；iPad 直放，用右下角的半透明方向鍵 (可以斜著走)。
- 戰機自動發射，對準正確答案的敵機。
- 可選 2～5、6～9 或全部的乘法表；簡單模式有點點圖提示。

**卡皮巴拉發音島**
- 六座小島：短母音、組合音、母音組合、看圖拼字、句型、最後大魔王。
- 點小島就會游過去，點朋友就會走過去聊天；地圖右上角「← 回樂園」。

## 專案結構

| 路徑 | 內容 |
|---|---|
| `Assets/Scripts/` | 樂園、注音怪獸大作戰、九九乘法戰機的程式 (場景全部用程式產生) |
| `Assets/PhonicsIsland/` | 卡皮巴拉發音島 (程式、單字圖片、音效、模型) |
| `Assets/Resources/phonic_symbols.txt` | 37 個注音符號 |
| `Assets/Resources/phonic_syllables.txt` | 題庫：拼音、例字、例詞 |
| `Assets/MoeAudio/` | 教育部辭典原始錄音 |
| `Assets/Resources/Audio/` | 裁切後遊戲用的錄音 (只留字音 / 詞音) |
| `Tools/download_moe_audio.ps1` | 依題庫核對讀音並下載辭典錄音 |
| `docs/` | 網頁版 (GitHub Pages) |

## 自己建置

需要 Unity 6000.6.7f1。

- `PlayGame.bat`：在這台電腦用瀏覽器玩網頁版 (會開一個本機小伺服器)。
- `BuildWeb.bat`：建置網頁版 (`Build/WebGL`，並複製到 `docs/` 給 GitHub Pages)。
- `BuildGame.bat`：建置 Windows 版 (`Build/PhonicShooter.exe`)。注意：Windows 11 開啟「智慧型應用程式控制」時，沒有簽章的 exe 會被擋住，請改玩網頁版。
- 九九乘法的題目和口訣語音由 `Tools/make_mul_voices.ps1` 用 Windows 中文語音產生，可以用同檔名的錄音覆蓋 (`Assets/Resources/MulVoice/`)。
- 改了題庫後先執行 `Tools/download_moe_audio.ps1` 下載錄音，建置時會自動裁切。

## 來源與授權

- 字音與詞音錄音：中華民國教育部《重編國語辭典修訂本》，CC BY-ND 3.0 TW，透過 [萌典](https://www.moedict.tw/) 取得。遊戲中的錄音經過裁切 (只留開頭的字音 / 詞音)，部分詞音由單字錄音接合而成。
- 中文字型：[Noto Sans TC](https://github.com/notofonts/noto-cjk)，SIL Open Font License 1.1 (`Assets/Resources/Fonts/NotoSansTC-LICENSE.txt`)。
- 網頁版內含家長自己錄的部分注音符號念法。
- 卡皮巴拉發音島 (來自 PhonicIsland)：
  - 3D 卡皮巴拉模型："Capybara" by Poly by Google，[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)，來源 <https://poly.pizza/m/66d-mKAgF17>（只調整顏色）
  - 音效：[Kenney](https://www.kenney.nl)（Interface Sounds、Impact Sounds、Music Jingles），CC0
  - 英文對話語音：Microsoft Edge 的 Natural 語音 (en-US-JennyNeural)，用 [edge-tts](https://github.com/rany2/edge-tts) 透過 Edge「大聲朗讀」的線上服務錄成音檔 (非微軟正式提供的用法)
  - 單字圖片：Microsoft Fluent Emoji 風格的表情符號，由 Windows 內建的 Segoe UI Emoji 字型繪製
  - 英文字型：Noto Sans TC 子集 (SIL OFL 1.1，`Assets/PhonicsIsland/Resources/Fonts/OFL.txt`)
