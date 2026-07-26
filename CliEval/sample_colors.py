# -*- coding: utf-8 -*-
"""Sample dominant colors of candidate idle clips for Chinese naming."""
import io
from pathlib import Path
from collections import Counter
from PIL import Image

ROOT = Path(r"c:\Users\jinji\Documents\GitHub\Ninegrid-Gambit")
SPR = ROOT / r"Assets/Arts/Images/Png/像素怪物合集/sprites"

FOLDERS = [
    "003skull_idle_01", "004skull_idle_01", "005skull_idle_01", "009skull_idle_01",
    "002bat_idle_01", "09bat_idle_01",
    "03slime_idle_01", "04slime_idle_01", "40slime_idle_01", "41slime_idle_01",
    "20bigslime_idle_01", "37goldenslime_idle_01",
    "06goblin_idle_01", "07goblin_idle_01", "08goblin_idle_01",
    "51LizardMan01_idle_01", "52LizardMan02_idle_01", "53LizardMan03_idle_01",
    "13wasp_idle_01", "02wolf_idle_01", "03wolf_new_idle_01",
    "06spider_idle_01", "39bigspider_idle_01", "18sandworm_idle_01",
    "17frog_idle_01", "31snake_idle_01", "28stegosaurus_idle_01",
    "08flower_idle_01", "16walkflower_idle_01", "50flowermonster_idle_01",
    "30treemonster_idle_01", "04mushroom_idle_01", "buff_bee_idle_01",
    "Map01_monster01_idle_01", "01rabit_new_idle_01", "01rabit_new_idle_02",
    "05mouse_idle_01", "10mouse_idle_01", "49buffalo_idle_01",
    "02wildboar_idle_01", "26wildboardknight_idle_01", "07bearBoss_attack_idle",
    "15WolfGobin_idle_01", "25WolfGobin_idle_01",
    "buff_golem_idle_01", "54tortoise_idle_01", "55tortoise_idle_01",
    "32crab_idle_01", "38pangolin_idle_01", "50ballcactus_idle_01",
    "34scorpion_idle_01", "21sandmonster_idle_01",
    "NPC_Dryad_golden_idle", "Sum_Elf_golden_idle_01",
]


def hue_name(r, g, b):
    import colorsys
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if v < 0.2:
        return "黑"
    if s < 0.15:
        return "白" if v > 0.7 else "灰"
    deg = h * 360
    if deg < 15 or deg >= 345:
        return "红"
    if deg < 45:
        return "橙/棕"
    if deg < 70:
        return "黄"
    if deg < 160:
        return "绿"
    if deg < 200:
        return "青"
    if deg < 260:
        return "蓝"
    if deg < 320:
        return "紫"
    return "粉红"


out = io.open(r"CliEval/color_names.txt", "w", encoding="utf-8")
for folder in FOLDERS:
    d = SPR / folder
    pngs = sorted(d.glob("*.png"))
    if not pngs:
        out.write(f"{folder}\tMISSING\n")
        continue
    im = Image.open(pngs[0]).convert("RGBA")
    counter = Counter()
    for px in im.getdata():
        r, g, b, a = px
        if a < 32:
            continue
        counter[hue_name(r, g, b)] += 1
    top = counter.most_common(3)
    out.write(f"{folder}\t{top}\n")
out.close()
print("done")
