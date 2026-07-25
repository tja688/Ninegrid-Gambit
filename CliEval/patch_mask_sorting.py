from pathlib import Path
import re

ROOT = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Prefabs")

MASK_SR = """  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 34
  m_MaskInteraction: 0
  m_Sprite: {fileID: 7482667652216324306, guid: 311925a002f4447b3a28927169b83ea6, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 0}"""

MASK_SR_OLD = MASK_SR.replace("34", "35")

MASK_SM = """  m_SortingOrder: 34
  m_MaskInteraction: 0
  m_Sprite: {fileID: 7482667652216324306, guid: 311925a002f4447b3a28927169b83ea6, type: 3}
  m_MaskAlphaCutoff: 0.1
  m_FrontSortingLayerID: 615528325
  m_BackSortingLayerID: 615528325
  m_FrontSortingLayer: 2
  m_BackSortingLayer: 2
  m_FrontSortingOrder: 66
  m_BackSortingOrder: 2"""

MASK_SM_OLD = MASK_SM.replace("34", "35").replace("66", "67").replace(" 2\n", " 3\n", 1)

for path in ROOT.glob("*.prefab"):
    if "标准" not in path.name:
        continue
    text = path.read_text(encoding="utf-8")
    if "主视图Mask" not in text and "\\u4E3B\\u89C6\\u56FEMask" not in text:
        continue
    new = text
    if MASK_SR_OLD in new:
        new = new.replace(MASK_SR_OLD, MASK_SR, 1)
    if MASK_SM_OLD in new:
        new = new.replace(MASK_SM_OLD, MASK_SM, 1)
    # Generic: mask placeholder SR on Default layer at 35 -> 34
    new = re.sub(
        r'(m_Name: "\\u4E3B\\u89C6\\u56FEMask".*?m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder:) 35',
        r"\g<1> 34",
        new,
        count=1,
        flags=re.S,
    )
    if new != text:
        path.write_text(new, encoding="utf-8")
        print("mask patch", path.name)
