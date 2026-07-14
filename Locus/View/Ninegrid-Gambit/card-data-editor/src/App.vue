<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { view } from "@locus/view-runtime";

interface CardRow {
  def_id: string;
  display_name: string;
  kind: string;
  rarity: string;
  price: number;
  level: number;
  is_elite: boolean;
  is_boss: boolean;
  is_reserve: boolean;
  deck_id: string;
  max_hp: number;
  attack: number;
  armor: number;
  recovery: number;
  tags: string;
  effect_ids: string;
  skill_ids: string;
}

const cards = ref<CardRow[]>([]);
const effectIds = ref<string[]>([]);
const skillIds = ref<string[]>([]);
const statusText = ref("就绪");
const loading = ref(false);
const saving = ref(false);
const error = ref("");
const kindFilter = ref("全部");
const searchText = ref("");
const editingCell = ref<{ row: number; col: string } | null>(null);
const editValue = ref("");

const kindOptions = ["全部", "HelpCard", "Monster"];

const filteredCards = computed(() => {
  let list = cards.value;
  if (kindFilter.value !== "全部") {
    list = list.filter((c) => c.kind === kindFilter.value);
  }
  if (searchText.value.trim()) {
    const q = searchText.value.trim().toLowerCase();
    list = list.filter(
      (c) =>
        c.def_id.toLowerCase().includes(q) ||
        c.display_name.toLowerCase().includes(q) ||
        c.tags.toLowerCase().includes(q)
    );
  }
  return list;
});

const countText = computed(() => {
  const total = cards.value.length;
  const shown = filteredCards.value.length;
  return total === shown ? `${total} 张` : `${shown} / ${total} 张`;
});

async function loadCards() {
  loading.value = true;
  error.value = "";
  statusText.value = "加载中...";
  try {
    const result = await view.callScript("CardEditorApi", "ReadCards");
    if (result.success) {
      cards.value = result.cards || [];
      effectIds.value = result.effectIds || [];
      skillIds.value = result.skillIds || [];
      statusText.value = `已加载 ${cards.value.length} 张卡牌`;
    } else {
      error.value = result.error || "加载失败";
      statusText.value = "加载失败";
    }
  } catch (e: any) {
    error.value = e.message || String(e);
    statusText.value = "加载出错";
  } finally {
    loading.value = false;
  }
}

async function saveCards() {
  saving.value = true;
  error.value = "";
  statusText.value = "保存中...";
  try {
    const result = await view.callScript("CardEditorApi", "WriteCards", {
      cards: JSON.parse(JSON.stringify(cards.value)),
    });
    if (result.success) {
      statusText.value = `已保存 ${cards.value.length} 张卡牌`;
    } else {
      error.value = result.error || "保存失败";
      statusText.value = "保存失败";
    }
  } catch (e: any) {
    error.value = e.message || String(e);
    statusText.value = "保存出错";
  } finally {
    saving.value = false;
  }
}

function startEdit(rowIndex: number, col: string, currentValue: any) {
  editingCell.value = { row: rowIndex, col };
  editValue.value = String(currentValue ?? "");
}

function commitEdit() {
  if (!editingCell.value) return;
  const { row, col } = editingCell.value;
  const card = filteredCards.value[row];
  if (!card) {
    editingCell.value = null;
    return;
  }

  const val = editValue.value;
  switch (col) {
    case "display_name":
    case "tags":
    case "effect_ids":
    case "skill_ids":
    case "deck_id":
      (card as any)[col] = val;
      break;
    case "price":
    case "level":
    case "max_hp":
    case "attack":
    case "armor":
    case "recovery":
      (card as any)[col] = parseInt(val, 10) || 0;
      break;
    case "is_elite":
    case "is_boss":
    case "is_reserve":
      (card as any)[col] = val === "true";
      break;
    case "rarity":
      card.rarity = val;
      break;
  }
  editingCell.value = null;
}

function cancelEdit() {
  editingCell.value = null;
}

function onEditKeydown(e: KeyboardEvent) {
  if (e.key === "Enter") commitEdit();
  if (e.key === "Escape") cancelEdit();
}

function rarityClass(rarity: string): string {
  switch (rarity) {
    case "White": return "rarity-white";
    case "Blue": return "rarity-blue";
    case "Gold": return "rarity-gold";
    case "Red": return "rarity-red";
    default: return "";
  }
}

onMounted(() => {
  loadCards();
});
</script>

<template>
  <main class="view-shell" data-locus-template="blank">
    <header class="view-toolbar">
      <div class="toolbar-title">
        <span>卡牌数据编辑器</span>
        <small>{{ statusText }}</small>
      </div>
      <div class="toolbar-actions">
        <select v-model="kindFilter">
          <option v-for="k in kindOptions" :key="k" :value="k">{{ k === "全部" ? "全部类型" : k }}</option>
        </select>
        <input
          v-model="searchText"
          type="text"
          placeholder="搜索名称/ID/标签..."
          class="search-input"
        />
        <button @click="loadCards" :disabled="loading">{{ loading ? "加载中..." : "刷新" }}</button>
        <button @click="saveCards" :disabled="saving" class="btn-save">{{ saving ? "保存中..." : "保存" }}</button>
      </div>
    </header>

    <section class="view-content">
      <div v-if="error" class="error-banner">{{ error }}</div>

      <div class="card-table-wrap">
        <table class="card-table">
          <thead>
            <tr>
              <th class="col-id">Def ID</th>
              <th class="col-name">名称</th>
              <th class="col-kind">类型</th>
              <th class="col-rarity">稀有度</th>
              <th class="col-num">价格</th>
              <th class="col-num">等级</th>
              <th class="col-num">HP</th>
              <th class="col-num">攻击</th>
              <th class="col-num">护甲</th>
              <th class="col-num">恢复</th>
              <th class="col-bool">精英</th>
              <th class="col-bool">Boss</th>
              <th class="col-bool">后备</th>
              <th class="col-tags">标签</th>
              <th class="col-ref">效果ID</th>
              <th class="col-ref">技能ID</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(card, idx) in filteredCards"
              :key="card.def_id"
              :class="[rarityClass(card.rarity), { 'row-monster': card.kind === 'Monster' }]"
            >
              <td class="col-id mono">{{ card.def_id }}</td>

              <td class="col-name" @dblclick="startEdit(idx, 'display_name', card.display_name)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'display_name'" class="edit-cell">
                  <input v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.display_name }}</span>
              </td>

              <td class="col-kind">{{ card.kind }}</td>

              <td class="col-rarity" @dblclick="startEdit(idx, 'rarity', card.rarity)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'rarity'" class="edit-cell">
                  <select v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus>
                    <option>White</option>
                    <option>Blue</option>
                    <option>Gold</option>
                    <option>Red</option>
                  </select>
                </span>
                <span v-else :class="['rarity-badge', rarityClass(card.rarity)]">{{ card.rarity }}</span>
              </td>

              <td class="col-num" @dblclick="startEdit(idx, 'price', card.price)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'price'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.price }}</span>
              </td>
              <td class="col-num" @dblclick="startEdit(idx, 'level', card.level)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'level'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.level }}</span>
              </td>
              <td class="col-num" @dblclick="startEdit(idx, 'max_hp', card.max_hp)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'max_hp'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.max_hp }}</span>
              </td>
              <td class="col-num" @dblclick="startEdit(idx, 'attack', card.attack)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'attack'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.attack }}</span>
              </td>
              <td class="col-num" @dblclick="startEdit(idx, 'armor', card.armor)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'armor'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.armor }}</span>
              </td>
              <td class="col-num" @dblclick="startEdit(idx, 'recovery', card.recovery)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'recovery'" class="edit-cell">
                  <input v-model="editValue" type="number" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.recovery }}</span>
              </td>

              <td class="col-bool" @dblclick="startEdit(idx, 'is_elite', card.is_elite)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'is_elite'" class="edit-cell">
                  <select v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus>
                    <option value="false">否</option>
                    <option value="true">是</option>
                  </select>
                </span>
                <span v-else :class="{ 'bool-true': card.is_elite }">{{ card.is_elite ? "✓" : "—" }}</span>
              </td>
              <td class="col-bool" @dblclick="startEdit(idx, 'is_boss', card.is_boss)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'is_boss'" class="edit-cell">
                  <select v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus>
                    <option value="false">否</option>
                    <option value="true">是</option>
                  </select>
                </span>
                <span v-else :class="{ 'bool-true': card.is_boss }">{{ card.is_boss ? "✓" : "—" }}</span>
              </td>
              <td class="col-bool" @dblclick="startEdit(idx, 'is_reserve', card.is_reserve)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'is_reserve'" class="edit-cell">
                  <select v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus>
                    <option value="false">否</option>
                    <option value="true">是</option>
                  </select>
                </span>
                <span v-else :class="{ 'bool-true': card.is_reserve }">{{ card.is_reserve ? "✓" : "—" }}</span>
              </td>

              <td class="col-tags" @dblclick="startEdit(idx, 'tags', card.tags)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'tags'" class="edit-cell">
                  <input v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.tags }}</span>
              </td>

              <td class="col-ref mono" @dblclick="startEdit(idx, 'effect_ids', card.effect_ids)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'effect_ids'" class="edit-cell">
                  <input v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.effect_ids || "—" }}</span>
              </td>

              <td class="col-ref mono" @dblclick="startEdit(idx, 'skill_ids', card.skill_ids)">
                <span v-if="editingCell?.row === idx && editingCell?.col === 'skill_ids'" class="edit-cell">
                  <input v-model="editValue" @keydown="onEditKeydown" @blur="commitEdit" autofocus />
                </span>
                <span v-else>{{ card.skill_ids || "—" }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="status-bar">
        <span>{{ countText }}</span>
        <span class="hint">双击单元格编辑 · 刷新加载 · 保存写入文件</span>
      </footer>
    </section>
  </main>
</template>
