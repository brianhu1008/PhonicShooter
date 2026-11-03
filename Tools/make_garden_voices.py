# 產生九九乘法花園的語音 (Edge Natural 台灣中文語音，edge-tts，要連網)：
#   Assets/Resources/GardenVoice/p_<a>_<b>.mp3  題目「種三排，每排四朵花」
#   Assets/Resources/GardenVoice/n_<k>.mp3      數數「十二」(1～81)
#   Assets/Resources/GardenVoice/c_<a>_<b>.mp3  口訣「三四十二」
#   ask / again / good1～3 / done               其他對話
# 用法：Tools\PhonicsIsland\ttsenv\Scripts\python.exe Tools\make_garden_voices.py
# (第一次先執行 Tools\PhonicsIsland\setup_tts.bat 安裝 edge-tts)。只錄還沒有的檔案。
import asyncio
import os
import sys

import edge_tts

VOICE = "zh-TW-HsiaoChenNeural"
RATE = "-10%"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "GardenVoice")
DIGITS = "零一二三四五六七八九"


def number(p):
    """10 → 十、12 → 十二、24 → 二十四"""
    if p < 10:
        return DIGITS[p]
    t, o = divmod(p, 10)
    return ("" if t == 1 else DIGITS[t]) + "十" + ("" if o == 0 else DIGITS[o])


def chant_number(p):
    """口訣裡的數字：10 → 一十 (要和 MulData.Number 一致)"""
    return "一十" if p == 10 else number(p)


def lines():
    # 語音會把「種」念成「種子」的三聲，所以用同音的「眾」(念 ㄓㄨㄥˋ)；
    # 「乘」也可能念錯，用同音的「成」(和 make_mul_voices.ps1 一樣)
    yield "plant", "在花圃上眾花，數數看！"
    yield "again", "再數數看！"
    yield "good1", "答對了！"
    yield "good2", "好棒！"
    yield "good3", "你好厲害！"
    yield "done", "花園眾滿了，你是九九乘法小園丁！"
    for k in range(1, 82):
        yield f"n_{k}", number(k)
    for a in range(2, 10):
        for b in range(1, 10):
            yield f"q_{a}_{b}", f"{DIGITS[a]}成以{DIGITS[b]}，等於多少？"
            yield f"p_{a}_{b}", f"眾{DIGITS[a]}排，每排{DIGITS[b]}朵花。"
            p = a * b
            # 加逗號，語音才會念成口訣的節奏，不會把「三四」念成「三十四」
            yield f"c_{a}_{b}", f"{DIGITS[a]}{DIGITS[b]}，{'得' + DIGITS[p] if p < 10 else chant_number(p)}。"


async def record(key, text, sem):
    path = os.path.join(OUT, key + ".mp3")
    async with sem:
        for attempt in range(4):
            try:
                await edge_tts.Communicate(text, VOICE, rate=RATE).save(path + ".tmp")
                os.replace(path + ".tmp", path)
                return True
            except Exception as e:
                print(f"retry {key}: {e}", file=sys.stderr)
                await asyncio.sleep(2 * (attempt + 1))
    return False


async def main():
    os.makedirs(OUT, exist_ok=True)
    todo = [(k, t) for k, t in lines() if not os.path.exists(os.path.join(OUT, k + ".mp3"))]
    sem = asyncio.Semaphore(4)
    results = await asyncio.gather(*(record(k, t, sem) for k, t in todo))
    failed = [k for (k, _), ok in zip(todo, results) if not ok]
    print(f"Recorded {len(todo) - len(failed)} garden voice lines")
    if failed:
        print("FAILED: " + " ".join(failed), file=sys.stderr)
        sys.exit(1)


asyncio.run(main())
