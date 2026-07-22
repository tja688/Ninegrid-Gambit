using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Editor.LivingUi
{
    internal enum LivingUiLayoutMode
    {
        /// <summary>保持大致位置，向四周贪心膨胀，直到碰到间隔/锁定/舞台边。</summary>
        InflateInPlace = 0,

        /// <summary>在舞台减去锁定障碍后的剩余空间里，按体型从大到小装填并撑满格子。</summary>
        PackFreeSpace = 1,

        /// <summary>无锁定时：按阅读序均分网格；有锁定时退化为剩余空间装填。</summary>
        EqualGrid = 2,
    }

    internal struct LivingUiLayoutSettings
    {
        public Rect Stage;
        public float IdealGap;
        public float StagePadding;
        public float LockClearance;
        public float FlowFloor;
        public bool PreserveAspect;
        public bool PixelSnap;
        public float PixelsPerUnit;
        public LivingUiLayoutMode Mode;
        public int InflatePasses;
        public float InflateStep;
    }

    internal struct LivingUiLayoutItem
    {
        public int Id;
        public Rect Rect;
        public bool Locked;
        public float Aspect;
    }

    internal static class LivingUiStageLayoutSolver
    {
        private enum Edge
        {
            Left,
            Right,
            Bottom,
            Top,
        }

        public static List<LivingUiLayoutItem> Solve(
            IReadOnlyList<LivingUiLayoutItem> input,
            in LivingUiLayoutSettings settings)
        {
            var items = new List<LivingUiLayoutItem>(input.Count);
            for (var i = 0; i < input.Count; i++)
            {
                var item = input[i];
                item.Rect = Sanitize(item.Rect, settings.FlowFloor);
                if (item.Aspect <= 0.0001f)
                {
                    item.Aspect = item.Rect.height > 0.0001f
                        ? item.Rect.width / item.Rect.height
                        : 1f;
                }

                items.Add(item);
            }

            var usable = Inset(settings.Stage, settings.StagePadding);
            if (usable.width < settings.FlowFloor || usable.height < settings.FlowFloor)
            {
                return items;
            }

            switch (settings.Mode)
            {
                case LivingUiLayoutMode.PackFreeSpace:
                    PackFreeSpace(items, usable, settings);
                    break;
                case LivingUiLayoutMode.EqualGrid:
                    if (CountLocked(items) == 0)
                    {
                        EqualGrid(items, usable, settings);
                    }
                    else
                    {
                        PackFreeSpace(items, usable, settings);
                    }

                    break;
                default:
                    InflateInPlace(items, usable, settings);
                    break;
            }

            if (settings.PixelSnap && settings.PixelsPerUnit > 0f)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.Locked)
                    {
                        continue;
                    }

                    item.Rect = LivingUiSlicedRectUtil.SnapRect(item.Rect, settings.PixelsPerUnit);
                    item.Rect = Sanitize(item.Rect, settings.FlowFloor);
                    items[i] = item;
                }
            }

            return items;
        }

        private static void InflateInPlace(
            List<LivingUiLayoutItem> items,
            Rect usable,
            in LivingUiLayoutSettings settings)
        {
            var step = Mathf.Max(0.01f, settings.InflateStep);
            if (settings.PixelSnap && settings.PixelsPerUnit > 0f)
            {
                step = Mathf.Max(step, 1f / settings.PixelsPerUnit);
            }

            var passes = Mathf.Max(1, settings.InflatePasses);
            for (var pass = 0; pass < passes; pass++)
            {
                var grewAny = false;
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].Locked)
                    {
                        continue;
                    }

                    if (settings.PreserveAspect)
                    {
                        grewAny |= TryGrowUniform(items, i, usable, settings, step);
                        continue;
                    }

                    grewAny |= TryGrowEdge(items, i, usable, settings, step, Edge.Left);
                    grewAny |= TryGrowEdge(items, i, usable, settings, step, Edge.Right);
                    grewAny |= TryGrowEdge(items, i, usable, settings, step, Edge.Bottom);
                    grewAny |= TryGrowEdge(items, i, usable, settings, step, Edge.Top);
                }

                if (!grewAny)
                {
                    break;
                }
            }
        }

        private static bool TryGrowUniform(
            List<LivingUiLayoutItem> items,
            int index,
            Rect usable,
            in LivingUiLayoutSettings settings,
            float step)
        {
            var item = items[index];
            var aspect = Mathf.Max(0.0001f, item.Aspect);
            float growW;
            float growH;
            if (aspect >= 1f)
            {
                growW = step;
                growH = step / aspect;
            }
            else
            {
                growH = step;
                growW = step * aspect;
            }

            var candidate = new Rect(
                item.Rect.xMin - growW * 0.5f,
                item.Rect.yMin - growH * 0.5f,
                item.Rect.width + growW,
                item.Rect.height + growH);

            if (!IsValidPlacement(candidate, index, items, usable, settings))
            {
                return false;
            }

            item.Rect = candidate;
            items[index] = item;
            return true;
        }

        private static bool TryGrowEdge(
            List<LivingUiLayoutItem> items,
            int index,
            Rect usable,
            in LivingUiLayoutSettings settings,
            float step,
            Edge edge)
        {
            var item = items[index];
            var candidate = edge switch
            {
                Edge.Left => new Rect(item.Rect.xMin - step, item.Rect.yMin, item.Rect.width + step, item.Rect.height),
                Edge.Right => new Rect(item.Rect.xMin, item.Rect.yMin, item.Rect.width + step, item.Rect.height),
                Edge.Bottom => new Rect(item.Rect.xMin, item.Rect.yMin - step, item.Rect.width, item.Rect.height + step),
                _ => new Rect(item.Rect.xMin, item.Rect.yMin, item.Rect.width, item.Rect.height + step),
            };

            if (!IsValidPlacement(candidate, index, items, usable, settings))
            {
                return false;
            }

            item.Rect = candidate;
            items[index] = item;
            return true;
        }

        private static void PackFreeSpace(
            List<LivingUiLayoutItem> items,
            Rect usable,
            in LivingUiLayoutSettings settings)
        {
            var free = new List<Rect> { usable };
            for (var i = 0; i < items.Count; i++)
            {
                if (!items[i].Locked)
                {
                    continue;
                }

                var blocker = ExpandForClearance(items[i].Rect, settings.LockClearance);
                free = SubtractRect(free, blocker);
            }

            var order = new List<int>();
            for (var i = 0; i < items.Count; i++)
            {
                if (!items[i].Locked)
                {
                    order.Add(i);
                }
            }

            order.Sort((a, b) =>
            {
                var areaA = items[a].Rect.width * items[a].Rect.height;
                var areaB = items[b].Rect.width * items[b].Rect.height;
                return areaB.CompareTo(areaA);
            });

            foreach (var index in order)
            {
                var item = items[index];
                var minW = Mathf.Max(settings.FlowFloor, settings.PreserveAspect
                    ? settings.FlowFloor * item.Aspect
                    : settings.FlowFloor);
                var minH = Mathf.Max(settings.FlowFloor, settings.PreserveAspect
                    ? settings.FlowFloor / Mathf.Max(0.0001f, item.Aspect)
                    : settings.FlowFloor);

                if (!TryPickFreeRect(free, minW, minH, out var cell, out var cellIndex))
                {
                    continue;
                }

                var placed = FitIntoCell(item, cell, settings);
                item.Rect = placed;
                items[index] = item;

                var occupied = ExpandForClearance(placed, settings.IdealGap * 0.5f);
                free.RemoveAt(cellIndex);
                free.AddRange(SplitRect(cell, occupied));
                free = PruneFree(free, settings.FlowFloor);
            }

            InflateInPlace(items, usable, settings);
        }

        private static void EqualGrid(
            List<LivingUiLayoutItem> items,
            Rect usable,
            in LivingUiLayoutSettings settings)
        {
            var unlocked = new List<int>();
            for (var i = 0; i < items.Count; i++)
            {
                if (!items[i].Locked)
                {
                    unlocked.Add(i);
                }
            }

            if (unlocked.Count == 0)
            {
                return;
            }

            unlocked.Sort((a, b) =>
            {
                var ra = items[a].Rect;
                var rb = items[b].Rect;
                var row = rb.center.y.CompareTo(ra.center.y);
                return row != 0 ? row : ra.center.x.CompareTo(rb.center.x);
            });

            var cols = Mathf.CeilToInt(Mathf.Sqrt(unlocked.Count));
            var rows = Mathf.CeilToInt(unlocked.Count / (float)cols);
            var gap = Mathf.Max(0f, settings.IdealGap);
            var cellW = (usable.width - gap * (cols - 1)) / cols;
            var cellH = (usable.height - gap * (rows - 1)) / rows;
            if (cellW < settings.FlowFloor || cellH < settings.FlowFloor)
            {
                return;
            }

            for (var n = 0; n < unlocked.Count; n++)
            {
                var col = n % cols;
                var row = n / cols;
                var x = usable.xMin + col * (cellW + gap);
                var y = usable.yMax - (row + 1) * cellH - row * gap;
                var cell = new Rect(x, y, cellW, cellH);
                var item = items[unlocked[n]];
                item.Rect = FitIntoCell(item, cell, settings);
                items[unlocked[n]] = item;
            }
        }

        private static Rect FitIntoCell(
            LivingUiLayoutItem item,
            Rect cell,
            in LivingUiLayoutSettings settings)
        {
            if (!settings.PreserveAspect)
            {
                return Sanitize(cell, settings.FlowFloor);
            }

            var aspect = Mathf.Max(0.0001f, item.Aspect);
            var w = cell.width;
            var h = w / aspect;
            if (h > cell.height)
            {
                h = cell.height;
                w = h * aspect;
            }

            w = Mathf.Max(settings.FlowFloor, w);
            h = Mathf.Max(settings.FlowFloor, h);
            return new Rect(
                cell.center.x - w * 0.5f,
                cell.center.y - h * 0.5f,
                w,
                h);
        }

        private static bool IsValidPlacement(
            Rect candidate,
            int index,
            List<LivingUiLayoutItem> items,
            Rect usable,
            in LivingUiLayoutSettings settings)
        {
            if (candidate.width < settings.FlowFloor || candidate.height < settings.FlowFloor)
            {
                return false;
            }

            if (candidate.xMin < usable.xMin - 0.0001f
                || candidate.yMin < usable.yMin - 0.0001f
                || candidate.xMax > usable.xMax + 0.0001f
                || candidate.yMax > usable.yMax + 0.0001f)
            {
                return false;
            }

            for (var j = 0; j < items.Count; j++)
            {
                if (j == index)
                {
                    continue;
                }

                var other = items[j];
                var gap = GapBetween(items[index].Locked, other.Locked, settings);
                if (RectsOverlap(candidate, other.Rect, gap))
                {
                    return false;
                }
            }

            return true;
        }

        private static float GapBetween(bool aLocked, bool bLocked, in LivingUiLayoutSettings settings)
        {
            if (aLocked && bLocked)
            {
                return 0f;
            }

            // 理想间隔只约束未锁定↔未锁定；触及锁定对象时用独立净空（默认 0）
            if (aLocked || bLocked)
            {
                return Mathf.Max(0f, settings.LockClearance);
            }

            return Mathf.Max(0f, settings.IdealGap);
        }

        private static bool RectsOverlap(Rect a, Rect b, float gap)
        {
            var pad = gap * 0.5f;
            var aa = new Rect(a.xMin - pad, a.yMin - pad, a.width + gap, a.height + gap);
            return aa.xMin < b.xMax
                   && aa.xMax > b.xMin
                   && aa.yMin < b.yMax
                   && aa.yMax > b.yMin;
        }

        private static Rect ExpandForClearance(Rect rect, float clearance)
        {
            if (clearance <= 0f)
            {
                return rect;
            }

            return new Rect(
                rect.xMin - clearance,
                rect.yMin - clearance,
                rect.width + clearance * 2f,
                rect.height + clearance * 2f);
        }

        private static Rect Inset(Rect rect, float padding)
        {
            padding = Mathf.Max(0f, padding);
            return new Rect(
                rect.xMin + padding,
                rect.yMin + padding,
                Mathf.Max(0f, rect.width - padding * 2f),
                Mathf.Max(0f, rect.height - padding * 2f));
        }

        private static Rect Sanitize(Rect rect, float floor)
        {
            return new Rect(
                rect.x,
                rect.y,
                Mathf.Max(floor, rect.width),
                Mathf.Max(floor, rect.height));
        }

        private static int CountLocked(List<LivingUiLayoutItem> items)
        {
            var n = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Locked)
                {
                    n++;
                }
            }

            return n;
        }

        private static bool TryPickFreeRect(
            List<Rect> free,
            float minW,
            float minH,
            out Rect best,
            out int bestIndex)
        {
            best = default;
            bestIndex = -1;
            var bestArea = -1f;
            for (var i = 0; i < free.Count; i++)
            {
                var r = free[i];
                if (r.width + 0.0001f < minW || r.height + 0.0001f < minH)
                {
                    continue;
                }

                var area = r.width * r.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = r;
                    bestIndex = i;
                }
            }

            return bestIndex >= 0;
        }

        private static List<Rect> SubtractRect(List<Rect> free, Rect blocker)
        {
            var result = new List<Rect>();
            foreach (var rect in free)
            {
                result.AddRange(SplitRect(rect, blocker));
            }

            return PruneFree(result, 0.01f);
        }

        private static List<Rect> SplitRect(Rect free, Rect solid)
        {
            var result = new List<Rect>();
            if (!free.Overlaps(solid))
            {
                result.Add(free);
                return result;
            }

            if (solid.xMin > free.xMin)
            {
                result.Add(Rect.MinMaxRect(free.xMin, free.yMin, solid.xMin, free.yMax));
            }

            if (solid.xMax < free.xMax)
            {
                result.Add(Rect.MinMaxRect(solid.xMax, free.yMin, free.xMax, free.yMax));
            }

            if (solid.yMin > free.yMin)
            {
                result.Add(Rect.MinMaxRect(
                    Mathf.Max(free.xMin, solid.xMin),
                    free.yMin,
                    Mathf.Min(free.xMax, solid.xMax),
                    solid.yMin));
            }

            if (solid.yMax < free.yMax)
            {
                result.Add(Rect.MinMaxRect(
                    Mathf.Max(free.xMin, solid.xMin),
                    solid.yMax,
                    Mathf.Min(free.xMax, solid.xMax),
                    free.yMax));
            }

            return result;
        }

        private static List<Rect> PruneFree(List<Rect> free, float minSize)
        {
            var result = new List<Rect>(free.Count);
            for (var i = 0; i < free.Count; i++)
            {
                var a = free[i];
                if (a.width < minSize || a.height < minSize)
                {
                    continue;
                }

                var contained = false;
                for (var j = 0; j < free.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var b = free[j];
                    if (b.xMin <= a.xMin && b.yMin <= a.yMin && b.xMax >= a.xMax && b.yMax >= a.yMax
                        && (b.width > a.width + 0.0001f || b.height > a.height + 0.0001f))
                    {
                        contained = true;
                        break;
                    }
                }

                if (!contained)
                {
                    result.Add(a);
                }
            }

            return result;
        }
    }
}
