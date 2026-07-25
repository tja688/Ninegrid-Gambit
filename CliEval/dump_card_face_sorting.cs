var path = NineGrid.Cards.CardChassisPaths.MonsterFacePrefab;
var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
if (root == null)
{
    return "missing " + path;
}

var inst = UnityEngine.Object.Instantiate(root);
var sb = new System.Text.StringBuilder();
var srs = inst.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
var mrs = inst.GetComponentsInChildren<UnityEngine.MeshRenderer>(true);
sb.AppendLine("path=" + path);
sb.AppendLine("SpriteRenderers=" + srs.Length + " MeshRenderers=" + mrs.Length);
for (var i = 0; i < srs.Length; i++)
{
    var r = srs[i];
    sb.AppendLine("SR " + BuildPath(inst.transform, r.transform) + " order=" + r.sortingOrder);
}

for (var i = 0; i < mrs.Length; i++)
{
    var r = mrs[i];
    sb.AppendLine("MR " + BuildPath(inst.transform, r.transform) + " order=" + r.sortingOrder);
}

var hits = NineGrid.Cards.Slots.CardFaceSortingOrderValidator.FindDuplicates(inst.transform);
sb.AppendLine("duplicates=" + hits.Count);
for (var i = 0; i < hits.Count; i++)
{
    sb.AppendLine(hits[i].ToString());
}

UnityEngine.Object.DestroyImmediate(inst);
return sb.ToString();

string BuildPath(UnityEngine.Transform rootT, UnityEngine.Transform node)
{
    var parts = new System.Collections.Generic.List<string>();
    var current = node;
    while (current != null)
    {
        parts.Add(current.name);
        if (current == rootT)
        {
            break;
        }

        current = current.parent;
    }

    parts.Reverse();
    return string.Join("/", parts);
}
