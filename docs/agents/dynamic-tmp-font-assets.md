# 动态 TMP 字体资产与 Git 提交

**不变量**：`m_AtlasPopulationMode: 1`（Dynamic）且 `m_ClearDynamicDataOnBuild: 1` 的 TMP 字体资产，仓库中必须保持“清空后”的规范状态（atlas 1×1、`m_GlyphTable: []`、`m_CharacterTable: []`，通常几百 KB）。**不要提交运行期填充快照**（1024×1024 atlas + 完整 glyph 表的 2 MB+ 版本）。

**原因**：Unity 6 的 TMP 在编辑器退出时会清空动态字体数据并重写 `.asset` 文件。若仓库提交了填充快照，每次干净退出后 git 都会出现该文件的 LFS pointer 差异，且无法通过反复提交根治。

**操作**：

- 若该文件显示为已修改且内容是“清空后”状态，直接提交即可（这是规范状态）。
- 若需要预烘焙字形以获得性能，应把字体资产改为 Static（`m_AtlasPopulationMode: 0`）并按需重新生成，而不是提交动态填充快照。

**案例**：2026-08-15 合并 `eb7a93f48` 曾误提交 `ChangBanDianSong-12 SDF.asset` 的填充快照，导致此后每次编辑器退出都产生 git 差异；2026-08-16 已恢复为清空后的规范状态。
