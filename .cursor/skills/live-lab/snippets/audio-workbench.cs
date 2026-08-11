// LiveLab 音频数据缝：以正式 audio_bindings.json 为底建 live 副本，热应用整包。
// 工作流：首跑建副本 → agent 编辑 live 副本（加/改绑定）→ 重跑本片段热应用 →
//         人试听 → 满意后落地 = live 副本 diff 合回正式 JSON。
// 注意：ApplyWorkbenchCatalog 是整包替换，所有临时绑定都改在同一份 live 副本里。
// 新素材必须先登记 audio_manifest.json 并清 AudioAssetManifestLoader 缓存，否则拒播。
if (!EditorApplication.isPlaying) return "需要在 Play 中执行";

string livePath = "Assets/Notes/LiveLab~/audio_bindings.live.json";
string basePath = "Assets/Resources/audio/audio_bindings.json";

if (!System.IO.File.Exists(livePath))
{
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(livePath));
    System.IO.File.Copy(basePath, livePath);
    return "[LiveLab] 已创建 live 副本: " + livePath + " —— 编辑它之后重跑本片段热应用";
}

string json = System.IO.File.ReadAllText(livePath);
var audio = NineGrid.Presentation.Systems.AudioSystem.EnsureRegistered();
var result = audio.ApplyWorkbenchCatalog(json);
if (!result.Succeeded) return "[LiveLab] 热应用失败(旧 catalog 未被替换): " + result.Error;

// 可选试听：把 bindingKey 换成要验证的键后取消注释
// （BindingKey = cueId + ␟ + cardDefId + ␟ + skillId + ␟ + roomId + ␟ + itemDefId + ␟ + contentId）
// var preview = audio.PreviewWorkbenchBinding("card.lifecycle.shuffle\u241f\u241f\u241f\u241f\u241f", true);
// Debug.Log("[LiveLab] preview: " + preview.Succeeded + " clip=" + preview.ActualClipKey);

return "[LiveLab] 热应用成功 revision=" + result.Revision;
