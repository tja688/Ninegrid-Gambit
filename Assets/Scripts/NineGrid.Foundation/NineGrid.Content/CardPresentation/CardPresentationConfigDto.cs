using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌内容/表现 JSON（JsonUtility 友好：无 Dictionary，字段 camelCase）。
    /// schemaVersion≥2：身份、数值、效果挂载与表现引用同文件，经
    /// <see cref="NineGrid.Content.ContentJsonCatalogProjector"/> 投影进
    /// <c>GameContentCatalog</c>（HelpCard/Monster/Skill/Relic/Deck/Room）。
    /// ChoiceOption / Avatar 等仅表现 contentId 可有 JSON，不进玩法 Catalog。
    /// 效果 DSL / 奖励池 / 经济 / 节点规则由 <c>ContentCatalogTableLoader</c> 加载（ADR-0008 / #69）。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationConfigDto
    {
        public int schemaVersion = 1;
        public string contentId;
        /// <summary>卡种类；投影以本字段为准（HelpCard/Monster/Skill/Relic/Deck/Room/…）。</summary>
        public string kind;
        /// <summary>deck 归属（全卡种必填语义）；决定卡背，特殊卡可在 sprites.back* 覆写。</summary>
        public string deckId;
        /// <summary>功能角色粗轴：Attack / Defense / Utility（代号主键）。</summary>
        public string role;
        /// <summary>显示名；schema≥2 投影写入 Catalog，已投影卡种不再由 Overlay 覆盖。</summary>
        public string displayName;
        /// <summary>
        /// 策划槽位名（仅表现/编辑器，不进 Core）：如「近战1」「远程2」「boss3」。
        /// 与 <see cref="sequence"/> 配套，交付主题卡组时供对照设计案。
        /// </summary>
        public string designSlotName;
        /// <summary>描述权威（有值时覆盖 TbContentVisual.description）；Relic/Skill 亦作 designText。</summary>
        public string description;
        /// <summary>
        /// 卡面介绍：人手写、纯跟卡面绑定（可含 {param} / [词条]）。
        /// 右键详述「背景介绍」框消费；不进卡面基础描述槽。
        /// </summary>
        public string faceIntro;
        /// <summary>
        /// 局内描述投影模板（ADR-0035）：实例可见表面（场上/手牌/道具格/遗物栏）与
        /// 店/奖/鉴预览渲染用的体感句，可含 <c>{装配id.键}</c> 装配参数引用；
        /// 空 = 无动态，投影与检查描述（<see cref="description"/>）同文。
        /// 右键检查永不展示本字段；倒计时/耐久卡必填（作者义务）。描述格硬上限 26。
        /// </summary>
        public string liveTemplate;
        /// <summary>
        /// 怪物攻击模式（ADR-0011）：普通近战 / 斜角近战 / 全向近战 / 普通远程 / 无。
        /// 必填；缺省为装配错误，不得静默当「无」。非 Monster 可空。
        /// </summary>
        public string attackPattern;
        /// <summary>金币：HelpCard→Price；Monster→KillGold；其它 Price（&gt;0）。</summary>
        public int gold;
        /// <summary>基础数值；schema≥2 投影写入；action 为攻击模式频率（ADR-0011）。</summary>
        public CardPresentationStatsDto stats;
        /// <summary>槽位图权威（有路径则不再读 ContentVisual SO）。</summary>
        public CardPresentationSpritesDto sprites;
        public CardPresentationMainVisualDto mainVisual;
        public CardPresentationAnimationsDto animations;
        public CardPresentationExtraSlotDto[] extraSlots;
        /// <summary>稀有度（如 White/Blue/Gold/Red）；schema≥2 时投影进 Catalog。</summary>
        public string rarity;
        /// <summary>标签列表；schema≥2 时投影进 Catalog。</summary>
        public string[] tags;
        /// <summary>
        /// 效果装配引用（ADR-0009 / #70）：templateId + 实参；投影时解析进 Catalog.Effects，
        /// 并把 mount id 写入 EffectIds。优先于 legacy <see cref="effectIds"/>。
        /// </summary>
        public EffectAssemblyDto[] effectAssemblies;
        /// <summary>legacy 效果挂载 id 列表；无 effectAssemblies 时仍投影。</summary>
        public string[] effectIds;
        /// <summary>怪物技能挂载；Monster schema≥2 投影进 Catalog.SkillIds。</summary>
        public string[] skillIds;
        /// <summary>
        /// 怪物等级：普通 / 层主（ADR-0022）。历史 int 1/2/3 已退役；投影只认中文或 FloorBoss/Normal。
        /// </summary>
        public string level;
        /// <summary>主题卡组内序列 1–5；序列 5 即层主。</summary>
        public int sequence;
        public bool isElite;
        public bool isBoss;
        public bool isReserve;
        /// <summary>技能容器类型（如 MonsterSkill）；Skill schema≥2。</summary>
        public string containerType;
        /// <summary>legacy：遭遇编排已迁至 tables/monster_decks.json，表现层不再写入。</summary>
        public string deckKind;
        /// <summary>legacy：遭遇成员改由卡面 deckId 归属推导，表现层不再写入。</summary>
        public string[] monsterDefIds;
        /// <summary>
        /// ADR-0032：卡级声明「可在非战斗相位（RoomChoice / RewardItemChoice）被使用」；
        /// 缺省 false = 战斗限定。标 true 的卡其全部装配模板目标须为 Player（卫生校验告警）。
        /// </summary>
        public bool usableOutsideBattle;
        /// <summary>房间权重与效果字段；Room schema≥2。</summary>
        public int weight;
        public string rewardPoolId;
        public int shopOfferCount;
        /// <summary>房间开局注入声明（ADR-0022）；空/缺省 = 显式无注入。</summary>
        public RoomOpeningInjectDto[] openingInjects;
        /// <summary>
        /// Room 场地图标预制体 Asset 路径（如 Assets/Resources/Prefabs/地形图标/商店图标.prefab）。
        /// 空则编辑器/后续投放回退 <c>CardChassisPaths.ResolveRoomIconPrefab</c>。
        /// </summary>
        public string iconPrefab;
        /// <summary>
        /// 场地图标落格（1–9）；0 = 未配置，运行时按单图标→2 / 双图标按选项序 1·3 回退。
        /// </summary>
        public int boardSlot;
    }

    [Serializable]
    public sealed class RoomOpeningInjectDto
    {
        public string side;
        public string source;
        public string cardDefId;
        public int count = 1;
        public bool allowDuplicates;
        public int monsterSequence;
        public RoomOpeningInjectPoolOptionDto[] pool;
    }

    [Serializable]
    public sealed class RoomOpeningInjectPoolOptionDto
    {
        public string cardDefId;
        public int weight;
    }

    [Serializable]
    public sealed class CardPresentationStatsDto
    {
        public int hp;
        public int armor;
        public int attack;
        public int action;
        /// <summary>怪物恢复；schema≥2 投影进 Catalog.Stats.Recovery。</summary>
        public int recovery;
    }

    [Serializable]
    public sealed class CardPresentationSpritesDto
    {
        public string mainIcon;
        public string faceBackground;
        public string cardFrame;
        public string banner;
        public string backBorder;
        public string backShirt;
        public string backLogo;
    }

    [Serializable]
    public sealed class CardPresentationMainVisualDto
    {
        public float offsetX;
        public float offsetY;
        public float uniformScale = 1f;
    }

    [Serializable]
    public sealed class CardPresentationAnimationsDto
    {
        public float defaultFps = 8f;
        public CardPresentationAnimSlotDto[] slots;
    }

    [Serializable]
    public sealed class CardPresentationAnimSlotDto
    {
        /// <summary>idle / attack / hurt / death / lunch</summary>
        public string id;
        /// <summary>none | folder | atlas</summary>
        public string sourceType;
        public string path;
        public float offsetX;
        public float offsetY;
    }

    [Serializable]
    public sealed class CardPresentationExtraSlotDto
    {
        public string code;
        public string path;
    }

    [Serializable]
    public sealed class CardPresentationVec2Dto
    {
        public float x;
        public float y;
    }

    /// <summary>卡上装配引用：模板 id + 实参（argsJson 为对象字面量，无默认值间接层）。</summary>
    [Serializable]
    public sealed class EffectAssemblyDto
    {
        /// <summary>挂载实例 id（写入 Catalog.EffectIds / Effects 键）。</summary>
        public string id;
        public string templateId;
        /// <summary>分类/检索用；不再做 typeTag 门禁。</summary>
        public string containerType;
        /// <summary>实参 JSON 对象，如 {"amount":10}。</summary>
        public string argsJson;
    }
}
