'use strict';

const fs = require('fs');
const path = require('path');

/** Canonical Task/subagent model IDs (must match Cursor model picker slugs). */
const ALLOWED_MODELS = Object.freeze({
  'cursor-grok-4.5-high': {
    family: 'grok',
    fast: false,
    display: 'Cursor Grok 4.5 High',
  },
  'cursor-grok-4.5-high-fast': {
    family: 'grok',
    fast: true,
    display: 'Cursor Grok 4.5 High Fast',
  },
  'composer-2.5': {
    family: 'composer',
    fast: false,
    display: 'Composer 2.5',
  },
  'composer-2.5-fast': {
    family: 'composer',
    fast: true,
    display: 'Composer 2.5 Fast',
  },
});

const STATE_DIR = path.join(__dirname, 'state');
const FAILURE_STATE_FILE = path.join(STATE_DIR, 'subagent-failures.json');
const DEBUG_LOG_FILE = path.join(STATE_DIR, 'subagent-hook.log');

function coerceToolInput(toolInput) {
  if (typeof toolInput === 'string') {
    try {
      return JSON.parse(toolInput);
    } catch {
      return {};
    }
  }
  if (toolInput && typeof toolInput === 'object') return toolInput;
  return {};
}

function parseHookInput(raw) {
  const text = String(raw ?? '')
    .replace(/^\uFEFF/, '')
    .trim();
  if (!text) return {};
  const input = JSON.parse(text);
  if (input && typeof input === 'object' && 'tool_input' in input) {
    return { ...input, tool_input: coerceToolInput(input.tool_input) };
  }
  return input;
}

function normalizeModel(raw) {
  if (typeof raw !== 'string') {
    return { canonical: null, raw: '' };
  }

  const trimmed = raw.trim();
  if (!trimmed) {
    return { canonical: null, raw: '' };
  }

  const lower = trimmed.toLowerCase();

  if (Object.prototype.hasOwnProperty.call(ALLOWED_MODELS, lower)) {
    return { canonical: lower, raw: trimmed };
  }

  const bracket = /^([^\[]+)\[(.+)\]$/i.exec(trimmed);
  const base = (bracket ? bracket[1] : trimmed).trim();
  const bracketBody = bracket ? bracket[2].trim().toLowerCase() : '';
  const baseLower = base.toLowerCase();

  const wantsFast =
    /(?:^|[-_])fast(?:$|[-_])/.test(baseLower) ||
    /fast\s*=\s*true/.test(bracketBody);
  const wantsSlow =
    /fast\s*=\s*false/.test(bracketBody) ||
    (!wantsFast && !/(?:^|[-_])fast(?:$|[-_])/.test(baseLower));

  if (/^composer[- ]?2(?:\.5)?(?:[- ]?fast)?$/i.test(base)) {
    if (wantsFast && !/fast\s*=\s*false/.test(bracketBody)) {
      return { canonical: 'composer-2.5-fast', raw: trimmed };
    }
    if (wantsSlow) {
      return { canonical: 'composer-2.5', raw: trimmed };
    }
  }

  if (/^cursor[- ]?grok[- ]?4(?:\.5)?[- ]?high(?:[- ]?fast)?$/i.test(base)) {
    if (wantsFast && !/fast\s*=\s*false/.test(bracketBody)) {
      return { canonical: 'cursor-grok-4.5-high-fast', raw: trimmed };
    }
    if (wantsSlow) {
      return { canonical: 'cursor-grok-4.5-high', raw: trimmed };
    }
  }

  if (/^grok[- ]?4(?:\.5)?[- ]?high(?:[- ]?fast)?$/i.test(base)) {
    if (wantsFast && !/fast\s*=\s*false/.test(bracketBody)) {
      return { canonical: 'cursor-grok-4.5-high-fast', raw: trimmed };
    }
    if (wantsSlow) {
      return { canonical: 'cursor-grok-4.5-high', raw: trimmed };
    }
  }

  return { canonical: null, raw: trimmed };
}

function extractModel(input, hookEventName) {
  if (!input || typeof input !== 'object') return '';

  const toolInput = coerceToolInput(input.tool_input ?? input.input);
  const toolName = String(input.tool_name || input.tool || '').toLowerCase();
  const isTask = toolName === 'task' || hookEventName === 'preToolUse';

  const candidates = isTask
    ? [toolInput.model, toolInput.subagent_model, input.subagent_model, input.model]
    : [input.subagent_model, toolInput.model, toolInput.subagent_model, input.model];

  for (const value of candidates) {
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }
  return '';
}

function extractSubagentType(input) {
  if (!input || typeof input !== 'object') return 'unknown';
  const toolInput = coerceToolInput(input.tool_input ?? input.input);
  return String(
    input.subagent_type || toolInput.subagent_type || toolInput.subagentType || 'unknown'
  );
}

function extractConversationId(input) {
  if (!input || typeof input !== 'object') return 'unknown';
  return String(
    input.conversation_id ||
      input.parent_conversation_id ||
      input.parentConversationId ||
      'unknown'
  );
}

function isAllowedModel(raw) {
  return normalizeModel(raw).canonical !== null;
}

function isFastCanonical(canonical) {
  return Boolean(ALLOWED_MODELS[canonical]?.fast);
}

function nonFastCanonical(canonical) {
  if (!canonical) return null;
  if (!isFastCanonical(canonical)) return canonical;
  if (canonical.endsWith('-fast')) {
    return canonical.slice(0, -5);
  }
  return null;
}

function ensureStateDir() {
  fs.mkdirSync(STATE_DIR, { recursive: true });
}

function readFailureState() {
  ensureStateDir();
  try {
    const parsed = JSON.parse(fs.readFileSync(FAILURE_STATE_FILE, 'utf8'));
    if (parsed && typeof parsed === 'object' && parsed.failures) {
      return parsed;
    }
  } catch {
    // ignore
  }
  return { failures: {} };
}

function writeFailureState(state) {
  ensureStateDir();
  fs.writeFileSync(FAILURE_STATE_FILE, JSON.stringify(state, null, 2));
}

function failureKey(conversationId, subagentType, baseModel) {
  return `${conversationId}:${subagentType}:${baseModel}`;
}

function hasRecordedFailure(conversationId, subagentType, baseModel) {
  const state = readFailureState();
  return Boolean(state.failures[failureKey(conversationId, subagentType, baseModel)]);
}

function recordFailure(conversationId, subagentType, modelRaw) {
  const normalized = normalizeModel(modelRaw);
  const baseModel = nonFastCanonical(normalized.canonical) || normalized.canonical;
  if (!baseModel) return;

  const state = readFailureState();
  state.failures[failureKey(conversationId, subagentType, baseModel)] = {
    at: new Date().toISOString(),
    model: normalized.canonical || modelRaw,
  };
  writeFailureState(state);
}

function appendDebug(entry) {
  try {
    ensureStateDir();
    fs.appendFileSync(DEBUG_LOG_FILE, `${JSON.stringify(entry)}\n`);
  } catch {
    // ignore
  }
}

function buildDeny(reason, userMessage, agentMessage) {
  return {
    permission: 'deny',
    user_message: userMessage,
    agent_message: agentMessage || userMessage,
    reason,
  };
}

function buildAllow() {
  return { permission: 'allow' };
}

function decideSubagentModelPolicy(input, hookEventName) {
  const modelRaw = extractModel(input, hookEventName);
  const subagentType = extractSubagentType(input);
  const conversationId = extractConversationId(input);

  appendDebug({
    ts: new Date().toISOString(),
    hookEventName,
    subagentType,
    modelRaw,
    conversationId,
  });

  if (!modelRaw) {
    // Inherit/settings path: user settings already pin explore default to grok high.
    return buildAllow();
  }

  const normalized = normalizeModel(modelRaw);
  if (!normalized.canonical) {
    return buildDeny(
      'model-not-allowlisted',
      `Subagent blocked: model "${modelRaw}" is not allowed. Use one of: cursor-grok-4.5-high, composer-2.5, cursor-grok-4.5-high-fast, composer-2.5-fast.`,
      `Task/subagent denied. Requested model "${modelRaw}" is outside the allowlist. ` +
        'Allowed: cursor-grok-4.5-high (hard tasks), composer-2.5 (simple tasks), ' +
        'cursor-grok-4.5-high-fast / composer-2.5-fast only after a non-fast attempt failed. ' +
        'If both fail, do the work in the parent agent instead of spawning another subagent.'
    );
  }

  if (isFastCanonical(normalized.canonical)) {
    const baseModel = nonFastCanonical(normalized.canonical);
    if (!hasRecordedFailure(conversationId, subagentType, baseModel)) {
      const meta = ALLOWED_MODELS[normalized.canonical];
      const baseMeta = ALLOWED_MODELS[baseModel];
      return buildDeny(
        'fast-without-prior-failure',
        `Subagent blocked: use non-fast "${baseMeta.display}" (${baseModel}) first; "${meta.display}" is only allowed after that attempt fails.`,
        `Fast subagent model "${normalized.canonical}" denied. Retry with "${baseModel}" first. ` +
          'Only after that non-fast subagent fails may you retry with the matching *-fast model. ' +
          'If both fail, stop spawning subagents and continue in the parent agent.'
      );
    }
  }

  return buildAllow();
}

module.exports = {
  ALLOWED_MODELS,
  appendDebug,
  buildAllow,
  buildDeny,
  coerceToolInput,
  decideSubagentModelPolicy,
  extractConversationId,
  extractModel,
  extractSubagentType,
  failureKey,
  hasRecordedFailure,
  isAllowedModel,
  isFastCanonical,
  nonFastCanonical,
  normalizeModel,
  parseHookInput,
  recordFailure,
};
