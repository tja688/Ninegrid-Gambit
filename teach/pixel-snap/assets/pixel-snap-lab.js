/**
 * Dual-panel lab: left = sprite (geometry snap), right = glyph (UV snap ruin).
 * Shared by lessons that teach geometry vs sample quantization.
 */
(function (global) {
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }

  function drawChecker(ctx, w, h, cell) {
    for (let y = 0; y < h; y += cell) {
      for (let x = 0; x < w; x += cell) {
        const on = ((x / cell) + (y / cell)) % 2 === 0;
        ctx.fillStyle = on ? "#1d1d1d" : "#141414";
        ctx.fillRect(x, y, cell, cell);
      }
    }
  }

  function snap(v, grid) {
    return Math.floor(v / grid) * grid + grid * 0.5;
  }

  function drawSprite(ctx, x, y, size, color) {
    ctx.fillStyle = color;
    ctx.fillRect(x, y, size, size);
    ctx.fillStyle = "#f5f5f5";
    ctx.fillRect(x + size * 0.25, y + size * 0.25, size * 0.2, size * 0.2);
  }

  /** Soft glyph via vertical alpha ramp — stands in for SDF edge softness. */
  function drawGlyph(ctx, x, y, w, h) {
    const g = ctx.createLinearGradient(x, y, x + w, y);
    g.addColorStop(0, "rgba(240,240,240,0)");
    g.addColorStop(0.35, "rgba(240,240,240,1)");
    g.addColorStop(0.65, "rgba(240,240,240,1)");
    g.addColorStop(1, "rgba(240,240,240,0)");
    ctx.fillStyle = g;
    ctx.fillRect(x, y, w, h);
  }

  function sampleQuantizeBlit(src, dst, block) {
    const sw = src.width;
    const sh = src.height;
    const sctx = src.getContext("2d");
    const dctx = dst.getContext("2d");
    const srcData = sctx.getImageData(0, 0, sw, sh);
    dctx.imageSmoothingEnabled = false;
    dctx.clearRect(0, 0, dst.width, dst.height);
    for (let y = 0; y < sh; y += block) {
      for (let x = 0; x < sw; x += block) {
        const sx = Math.min(sw - 1, Math.floor(x / block) * block + Math.floor(block / 2));
        const sy = Math.min(sh - 1, Math.floor(y / block) * block + Math.floor(block / 2));
        const i = (sy * sw + sx) * 4;
        dctx.fillStyle = `rgba(${srcData.data[i]},${srcData.data[i + 1]},${srcData.data[i + 2]},${srcData.data[i + 3] / 255})`;
        dctx.fillRect(x, y, block, block);
      }
    }
  }

  function mount(root) {
    if (!root) return;
    root.innerHTML = `
      <div class="lab-controls">
        <label><input type="checkbox" data-role="vertex" checked> Sprite 顶点 snap</label>
        <label><input type="checkbox" data-role="uv"> 全屏 UV snap（打到字）</label>
        <label>亚像素偏移 <input type="range" data-role="offset" min="0" max="7" value="3"></label>
      </div>
      <canvas width="640" height="220" aria-label="geometry vs uv snap lab"></canvas>
      <div class="caption" data-role="caption"></div>
    `;

    const canvas = root.querySelector("canvas");
    const ctx = canvas.getContext("2d");
    const vertexToggle = root.querySelector('[data-role="vertex"]');
    const uvToggle = root.querySelector('[data-role="uv"]');
    const offset = root.querySelector('[data-role="offset"]');
    const caption = root.querySelector('[data-role="caption"]');

    const offscreen = document.createElement("canvas");
    offscreen.width = 320;
    offscreen.height = 220;
    const octx = offscreen.getContext("2d");

    function render() {
      const grid = 8;
      const sub = Number(offset.value);
      drawChecker(ctx, canvas.width, canvas.height, grid);

      // Left panel: sprite
      let sx = 48 + sub;
      let sy = 72 + sub;
      if (vertexToggle.checked) {
        sx = snap(sx, grid);
        sy = snap(sy, grid);
      }
      drawSprite(ctx, sx, sy, 48, "#3aa0ff");
      ctx.fillStyle = "#9ad0ff";
      ctx.font = "14px Segoe UI, sans-serif";
      ctx.fillText("Sprite", 48, 28);

      // Right panel: glyph rendered then optionally UV-quantized
      octx.clearRect(0, 0, offscreen.width, offscreen.height);
      drawChecker(octx, offscreen.width, offscreen.height, grid);
      const gx = 120 + sub * 0.5;
      const gy = 70 + sub * 0.5;
      drawGlyph(octx, gx, gy, 18, 64);
      octx.fillStyle = "#ddd";
      octx.font = "14px Segoe UI, sans-serif";
      octx.fillText("Glyph (SDF stand-in)", 48, 28);

      if (uvToggle.checked) {
        const quant = document.createElement("canvas");
        quant.width = offscreen.width;
        quant.height = offscreen.height;
        sampleQuantizeBlit(offscreen, quant, grid);
        ctx.drawImage(quant, 320, 0);
      } else {
        ctx.drawImage(offscreen, 320, 0);
      }

      ctx.strokeStyle = "#444";
      ctx.beginPath();
      ctx.moveTo(320.5, 0);
      ctx.lineTo(320.5, canvas.height);
      ctx.stroke();

      const v = vertexToggle.checked ? "开" : "关";
      const u = uvToggle.checked ? "开" : "关";
      caption.textContent =
        `顶点 snap ${v} · 全屏 UV snap ${u} · 亚像素 ${sub}px。` +
        (uvToggle.checked
          ? " 右栏：整块屏幕被按格重采样 → 细边缘糊成台阶。"
          : " 右栏：字形边缘仍是连续渐变（模拟 SDF AA）。");
    }

    [vertexToggle, uvToggle, offset].forEach((el) => el.addEventListener("input", render));
    render();
  }

  global.PixelSnapLab = { mount, clamp };
})(window);
