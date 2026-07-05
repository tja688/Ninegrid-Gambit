using NineGrid.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace NineGrid.UI
{
    /// <summary>
    /// 全局 UI 单例，挂在 UI 预制体根节点，跨场景不销毁，统一调控各 UI 部件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSystem : MonoBehaviour
    {
        public static UiSystem Instance { get; private set; }

        [Header("Roots")]
        [SerializeField] GameObject overlayRoot;
        [SerializeField] SpriteRenderer portraitRenderer;
        [SerializeField] SpriteRenderer dialogBoxRenderer;
        [SerializeField] GameObject continueArrow;

        [Header("Texts")]
        [SerializeField] GameObject dialogTextObject;
        [SerializeField] GameObject noticeTextObject;
        [SerializeField] GameObject factoryTextObject;

        [Header("Systems")]
        [SerializeField] DialogueSystem dialogueSystem;
        [SerializeField] NoticeSystem noticeSystem;
        [SerializeField] EnemyIntroducePanelController enemyIntroducePanel;
        [SerializeField] EnemyHpBarController enemyHpBar;

        public DialogueSystem Dialogue => dialogueSystem;
        public NoticeSystem Notice => noticeSystem;
        /// <summary>敌人信息面板（Enemy Info Panel）。</summary>
        public EnemyIntroducePanelController EnemyInfo => enemyIntroducePanel;
        public EnemyIntroducePanelController EnemyIntroduce => enemyIntroducePanel;
        public EnemyHpBarController EnemyHp => enemyHpBar;
        public SpriteRenderer Portrait => portraitRenderer;
        public SpriteRenderer DialogBox => dialogBoxRenderer;
        public GameObject OverlayRoot => overlayRoot;
        public GameObject DialogTextObject => dialogTextObject;
        public GameObject NoticeTextObject => noticeTextObject;
        public GameObject FactoryTextObject => factoryTextObject;
        public GameObject ContinueArrow => continueArrow;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystemPersists();
            CacheReferencesIfNeeded();
            HideAllImmediate();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 其他场景若残留同名 Overlay UI，运行时清掉，避免与全局 UI 冲突。
            StripDuplicateOverlayUi(scene);
        }

        public void SetOverlayActive(bool active)
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(active);
            }
        }

        public void SetPortraitActive(bool active)
        {
            if (portraitRenderer != null)
            {
                portraitRenderer.gameObject.SetActive(active);
            }
        }

        public void SetDialogBoxActive(bool active)
        {
            if (dialogBoxRenderer != null)
            {
                dialogBoxRenderer.gameObject.SetActive(active);
            }
        }

        public void SetDialogTextActive(bool active)
        {
            if (dialogTextObject != null)
            {
                dialogTextObject.SetActive(active);
            }
        }

        public void SetNoticeTextActive(bool active)
        {
            if (noticeTextObject != null)
            {
                noticeTextObject.SetActive(active);
            }
        }

        public void SetFactoryTextActive(bool active)
        {
            if (factoryTextObject != null)
            {
                factoryTextObject.SetActive(active);
            }
        }

        public void SetContinueArrowActive(bool active)
        {
            if (continueArrow != null)
            {
                continueArrow.SetActive(active);
            }
        }

        public void HideAllImmediate()
        {
            SetDialogTextActive(false);
            SetNoticeTextActive(false);
            SetFactoryTextActive(false);
            SetContinueArrowActive(false);
            SetPortraitActive(false);
            SetDialogBoxActive(false);
            SetOverlayActive(false);
        }

        public GameObject GetNoticeChannelObject(NoticeChannel channel)
        {
            return channel switch
            {
                NoticeChannel.Factory => factoryTextObject,
                _ => noticeTextObject,
            };
        }

        void CacheReferencesIfNeeded()
        {
            if (overlayRoot == null)
            {
                overlayRoot = FindChild("Overlay")?.gameObject;
            }

            if (portraitRenderer == null)
            {
                portraitRenderer = FindChild("Portrait")?.GetComponent<SpriteRenderer>();
            }

            if (dialogBoxRenderer == null)
            {
                dialogBoxRenderer = FindChild("Dialog Box")?.GetComponent<SpriteRenderer>();
            }

            if (continueArrow == null)
            {
                continueArrow = FindChild("指向箭头")?.gameObject;
            }

            if (dialogTextObject == null)
            {
                dialogTextObject = FindChild("Dialog Text")?.gameObject;
            }

            if (noticeTextObject == null)
            {
                noticeTextObject = FindChild("NoticeText")?.gameObject;
            }

            if (factoryTextObject == null)
            {
                factoryTextObject = FindChild("FactoryText")?.gameObject;
            }

            if (dialogueSystem == null)
            {
                dialogueSystem = GetComponent<DialogueSystem>();
            }

            if (noticeSystem == null)
            {
                noticeSystem = GetComponent<NoticeSystem>();
            }

            if (enemyIntroducePanel == null)
            {
                enemyIntroducePanel = GetComponent<EnemyIntroducePanelController>();
            }

            if (enemyHpBar == null)
            {
                enemyHpBar = GetComponentInChildren<EnemyHpBarController>(true);
            }
        }

        Transform FindChild(string trimmedName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t != null && t.name.Trim() == trimmedName)
                {
                    return t;
                }
            }

            return null;
        }

        static void EnsureEventSystemPersists()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>();
            }

            if (eventSystem != null)
            {
                DontDestroyOnLoad(eventSystem.gameObject);
            }
        }

        static void StripDuplicateOverlayUi(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                if (root.name == "Overlay UI" || root.name.Trim() == "Overlay UI")
                {
                    Destroy(root);
                    continue;
                }

                // 场景内又放了一份 UI 预制体时，交给 Awake 单例销毁；这里兜底清名称为 UI 且非本实例的根。
                if (root.name == "UI" && Instance != null && root != Instance.gameObject)
                {
                    Destroy(root);
                }
            }
        }
    }
}
