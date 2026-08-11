#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.DevTest.Flow
{
    [DisallowMultipleComponent]
    public sealed class GoldGainFxManagerDevKeys : TestKeyModuleBehaviour
    {
        protected override string ModuleId => "gold-gain-fx";

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Keypad9, "屏幕中心飞入10金币", () => SpawnTenAtScreenCenter());
        }

        private void SpawnTenAtScreenCenter()
        {
            GoldGainPresentationBinder.EnsureInstalled();
            var hud = PlayerInfoHudPresenter.TryGetInstance();
            var before = hud != null ? hud.DisplayedGold : 0;
            GoldGainPresentationBinder.PresentGainVisual(10, before + 10, ResolveScreenCenterWorld());
        }

        private static Vector3 ResolveScreenCenterWorld()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var depth = camera.orthographic
                    ? Mathf.Abs(camera.transform.position.z)
                    : camera.nearClipPlane;
                var world = camera.ScreenToWorldPoint(
                    new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, depth));
                world.z = 0f;
                return world;
            }

            return Vector3.zero;
        }
    }
}

#endif
