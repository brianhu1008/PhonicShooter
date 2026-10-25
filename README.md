# 注音怪獸大作戰

幫小朋友認識注音符號和拼音的第一人稱射擊遊戲。角色會沿著森林小路自動往前走，遇到一群舉著注音牌子的怪獸，打倒正確的那一隻就會念出拼讀，例如「ㄐ → ㄧ → ㄣ → 金 → 金色」。

**👉 直接在瀏覽器玩（iPad 用 Safari 也可以）：https://brianhu1008.github.io/PhonicShooter/**

iPad 上可以按 Safari 的「分享 → 加入主畫面」，之後就像 App 一樣全螢幕開啟。

## 玩法

- 用滑鼠移動準星、按左鍵發射；iPad 直接點怪獸。
- 37 個注音符號全部都有，題庫有 204 個常用字的拼音和例詞。
- 四種玩法：認識注音、聽一聽拼一拼、看字找注音、耳朵大挑戰。
- 簡單 / 困難兩種難度。怪獸會靠近並出拳、踢腳，血條歸零就輸了；答對會補血。
- 電腦版可以在「錄製注音念法」用學校教的念法自己錄每個注音符號。

## 專案結構

| 路徑 | 內容 |
|---|---|
| `Assets/Scripts/` | 遊戲程式 (場景、怪獸、玩家、介面全部用程式產生) |
| `Assets/Resources/phonic_symbols.txt` | 37 個注音符號 |
| `Assets/Resources/phonic_syllables.txt` | 題庫：拼音、例字、例詞 |
| `Assets/MoeAudio/` | 教育部辭典原始錄音 |
| `Assets/Resources/Audio/` | 裁切後遊戲用的錄音 (只留字音 / 詞音) |
| `Tools/download_moe_audio.ps1` | 依題庫核對讀音並下載辭典錄音 |
| `docs/` | 網頁版 (GitHub Pages) |

## 自己建置

需要 Unity 6000.6.7f1。

- `BuildGame.bat`：建置 Windows 版 (`Build/PhonicShooter.exe`)，`PlayGame.bat` 直接開始玩。
- `BuildWeb.bat`：建置網頁版 (`Build/WebGL`)。
- 改了題庫後先執行 `Tools/download_moe_audio.ps1` 下載錄音，建置時會自動裁切。

## 來源與授權

- 字音與詞音錄音：中華民國教育部《重編國語辭典修訂本》，CC BY-ND 3.0 TW，透過 [萌典](https://www.moedict.tw/) 取得。遊戲中的錄音經過裁切 (只留開頭的字音 / 詞音)，部分詞音由單字錄音接合而成。
- 中文字型：[Noto Sans TC](https://github.com/notofonts/noto-cjk)，SIL Open Font License 1.1 (`Assets/Resources/Fonts/NotoSansTC-LICENSE.txt`)。
- 網頁版內含家長自己錄的部分注音符號念法。
