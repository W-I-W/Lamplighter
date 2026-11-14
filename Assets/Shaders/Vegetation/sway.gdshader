// Wind sway for trees, bushes and grass drawn as TileMapLayer tiles.
// Only tiles listed in amp[] move (indexed by the atlas column of the tile); every other tile is drawn unchanged, so the layer itself is never deformed.
// The top of a sprite moves most, the bottom stays put; displacement is rounded to whole pixels to keep the pixel grid intact.
shader_type canvas_item;

uniform sampler2D noise_tex : filter_linear, repeat_enable;
uniform sampler2D tile_data : filter_nearest, repeat_disable;   // one texel per atlas column: r = max sway (px), g = first opaque row (sprite top), b = glow
uniform float wind = 1.0;          // overall wind strength (config * player setting)
uniform vec2 player = vec2(-99999.0);   // world position of the player (grass bends away)
uniform vec4 shakes[16];           // x, y = tile coords, z = amplitude (px, already decayed), w = phase (+100 = удар по кроне: двигает только колонки крон)
uniform float tree_cut[9];                 // строка тайла, с которой начинается ствол (выше — крона), по видам деревьев; виды — большие тайлы 32×32 с левым столбцем 182, 184, ... 198
varying vec2 wpos;
varying vec4 vcol;                 // модуляция слоя (прозрачность крон за игроком)

float hash(vec2 p) { return fract(sin(dot(p, vec2(41.7, 289.3))) * 4375.5453); }

// kind = floor(w / 100): 0 блок, 1 крона дерева, 2 ствол дерева. Удар по дереву сдвигает ствол и крону ОДНОЙ плавной кривой по высоте всего дерева (два тайла),
// поэтому на стыке ствола и кроны шва нет; чужой ствол в клетке кроны не затрагивается.
float shake_gate(float w, float col) {
	bool is_crown = false;
	float kind = floor(w / 100.0 + 0.001);
	return (kind == 1.0) == is_crown ? 1.0 : 0.0;
}
// доля высоты дерева от земли (0) до верхушки кроны (1) для строки ly тайла (0 = верх тайла, 1 = низ)
float tree_h(float w, float ly) {
	float kind = floor(w / 100.0 + 0.001);
	float hb = kind == 1.0 ? 0.5 + (1.0 - ly) * 0.5 : (1.0 - ly) * 0.5;
	return hb * hb;
}

void vertex() {
	vcol = COLOR;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	// Удар по камню, руде и любому другому блоку 16×16: наклоняется сама клетка (верхние вершины уезжают, низ стоит), а не картинка внутри неё.
	// Раньше сдвигались UV, и всё, что выезжало за край клетки, срезалось: у камней шириной во всю клетку пропадал бок на 2–4 px.
	// Углы прямоугольника тайла идут в порядке (0,0) (0,1) (1,1) (1,0) [(0,0) (1,1)]: по номеру вершины понятно, верхняя она или нижняя, левая или правая.
	int vid = VERTEX_ID % 6;
	bool v_top = vid == 0 || vid == 3 || vid == 4;
	bool v_right = vid == 2 || vid == 3 || vid == 5;
	float vcolumn = floor(UV.x / (TEXTURE_PIXEL_SIZE.x * 16.0) + 0.5) - (v_right ? 1.0 : 0.0);
	bool v_tree = vcolumn >= 181.5 && vcolumn <= 199.5;
	vec4 vtd = textureLod(tile_data, vec2((vcolumn + 0.5) / 256.0, 0.5), 0.0);
	if (v_top && !v_tree && vtd.r <= 0.0) {
		vec2 vcell = floor((wpos + vec2(v_right ? -8.0 : 8.0, 8.0)) / 16.0);
		float rock = 0.0;
		for (int i = 0; i < 16; i++) {
			vec4 s = shakes[i];
			if (s.z > 0.0 && s.w < 200.0 && abs(s.x - vcell.x) < 0.5 && abs(s.y - vcell.y) < 0.5 && shake_gate(s.w, vcolumn) > 0.0) rock += s.z * sin(TIME * 48.0 + s.w);
		}
		// основание спрайта стоит, его верхняя непрозрачная строка (vtd.g) сдвигается на rock пикселей — та же кривая, что была у сдвига UV
		VERTEX.x += rock / max(1.0 - vtd.g / 16.0, 0.1);
	}
}

void fragment() {
	vec2 atlas = vec2(textureSize(TEXTURE, 0));
	float col = floor(UV.x * atlas.x / 16.0 + 0.001);
	vec4 td = texture(tile_data, vec2((col + 0.5) / 256.0, 0.5));
	float a = td.r;
	vec2 uv = UV;
	// ---- дерево: один тайл 32×32 (крона и ствол вместе). Качание от ветра — покадровая анимация атласа (кадры нарисованы в PNG); здесь только удар: крона целиком сдвигается на целые пиксели ----
	float tbase = -1.0; float tcut = 22.0;
	if (col >= 181.5 && col <= 199.5) {
		float ti = floor((col - 182.0) * 0.5 + 0.001);
		tbase = 182.0 + ti * 2.0; tcut = tree_cut[int(ti)];
	}
	if (tbase >= 0.0) {
		bool right = col > tbase + 0.5;
		float ypix = mod(UV.y * atlas.y, 32.0);                           // строка внутри кадра: 0 — макушка, 32 — корень (кадры лежат столбиком по 32 px)
		float cxx = floor((wpos.x + (right ? -8.0 : 8.0)) / 16.0);        // клетка ствола: тайл центрирован по X на клетке, нижняя половина — клетка ствола
		float cyy = floor(wpos.y / 16.0) + (ypix < 16.0 ? 1.0 : 0.0);
		vec2 tcell = vec2(cxx, cyy);
		float tshift = 0.0;
		for (int i = 0; i < 16; i++) {
			vec4 sh = shakes[i];
			float kind = floor(sh.w / 100.0 + 0.001);
			if (sh.z > 0.0 && kind > 1.5 && abs(sh.x - tcell.x) < 0.5 && abs(sh.y - tcell.y) < 0.5) tshift += sh.z * sin(TIME * 48.0 + sh.w - kind * 100.0);
		}
		float tpx = ypix < tcut ? floor(tshift + 0.5) : 0.0;               // крона целиком, на целое число пикселей: ни один пиксель не «уезжает» от соседей
		vec2 tuv = UV; tuv.x = UV.x - tpx / atlas.x;
		float tlo = tbase * 16.0 / atlas.x; float thi = (tbase + 2.0) * 16.0 / atlas.x;
		float tin = (tuv.x >= tlo && tuv.x < thi) ? 1.0 : 0.0;
		tuv.x = clamp(tuv.x, tlo + 0.5 / atlas.x, thi - 0.5 / atlas.x);
		COLOR = texture(TEXTURE, tuv) * vcol * tin;
	} else
	if (a > 0.0) {
		vec2 cell = floor(wpos / 16.0);
		float ph = hash(cell) * 6.2831;
		float sv = 0.75 + 0.5 * hash(cell + 7.0);
		float gust = texture(noise_tex, wpos / 700.0 + vec2(TIME * 0.012, 0.0)).r;
		float strength = wind * (0.45 + 1.1 * smoothstep(0.35, 0.8, gust));
		float sway = sin(TIME * 1.7 * sv + ph + wpos.x * 0.015) * strength + 0.35 * sin(TIME * 3.1 * sv + ph * 1.7) * strength;
		float ly = fract(UV.y * atlas.y / 16.0);                 // 0 = top of the tile, 1 = bottom
		// the base of the plant stays fixed, the tip moves most: 0 at the bottom row, 1 at the first opaque row of the sprite
		float tr = td.g / 16.0;
		// bend curve taken from the CapyCam "Tree & Grass Motion" shader (smoothstep * power), normalised so the tip of the sprite = 1 and the root = 0
		float yy = clamp((ly - tr) / max(1.0 - tr, 0.1), 0.0, 1.0);          // 0 at the first opaque row, 1 at the bottom
		float lift = smoothstep(0.3, 1.3, pow(1.0 - yy, 0.5)) * pow(2.0 - yy, 1.5) / (0.784 * 2.83);

		float shift = a * sway;
		// pushed by the player
		vec2 centre = (cell + 0.5) * 16.0;
		vec2 d = centre - player;
		float dl = length(d);
		if (dl < 14.0) shift += sign(d.x) * (1.0 - dl / 14.0) * min(a, 2.5) * 1.6 * wind;
		// axe/pickaxe hits
		float dsh = 0.0;
		float lyy = fract(UV.y * atlas.y / 16.0);
		for (int i = 0; i < 16; i++) {
			vec4 s = shakes[i];
			if (s.z > 0.0 && abs(s.x - cell.x) < 0.5 && abs(s.y - cell.y) < 0.5) dsh += s.z * sin(TIME * 48.0 + s.w) * shake_gate(s.w, col) * tree_h(s.w, lyy);
		}
		if (td.a > 0.0) {
			// hand-drawn poses: pick the upright tile or one of four bent tiles stored next to it in the atlas (whole sprite pixels, no distortion)
			float k = clamp(floor(shift * 2.0 / a + 0.5), -2.0, 2.0);
			float pose_col = k == 0.0 ? col : td.a + (k < -1.5 ? 0.0 : k < 0.0 ? 1.0 : k < 1.5 ? 2.0 : 3.0);
			uv.x = UV.x + (pose_col - col) * 16.0 / atlas.x - dsh * 1.9 / atlas.x;
			float plo = pose_col * 16.0 / atlas.x + 0.5 / atlas.x; float phi = (pose_col + 1.0) * 16.0 / atlas.x - 0.5 / atlas.x;
			float inside = (uv.x >= plo - 0.5 / atlas.x && uv.x <= phi + 0.5 / atlas.x) ? 1.0 : 0.0;       // за краем тайла — пусто, а не размазанный крайний столбец (давал тёмный прямоугольник)
			uv.x = clamp(uv.x, plo, phi);
			COLOR = texture(TEXTURE, uv) * vcol * inside;
		} else {
		float px = shift * lift;          // fractional on purpose: with nearest sampling and a 3x zoom the blade edges then move in single screen pixels (smooth), not in 3-pixel jumps
		uv.x = UV.x - (px + dsh) / atlas.x;
		float lo = col * 16.0 / atlas.x + 0.5 / atlas.x;
		float hi = (col + 1.0) * 16.0 / atlas.x - 0.5 / atlas.x;
		uv.x = clamp(uv.x, lo, hi);                                // edge pixels are repeated instead of cut off, so the silhouette never gets sliced
		COLOR = texture(TEXTURE, uv) * vcol;
		}
	} else {
		// камни, руда и прочие блоки не качаются на ветру, но при ударе раскачиваются: основание на месте, верхушка ходит
		float rock = 0.0, trunk = 0.0, trunk_out = 1.0;
		vec2 cell = floor(wpos / 16.0);
		float lyy = fract(UV.y * atlas.y / 16.0);
		for (int i = 0; i < 16; i++) {
			vec4 s = shakes[i];
			if (s.z > 0.0 && abs(s.x - cell.x) < 0.5 && abs(s.y - cell.y) < 0.5 && shake_gate(s.w, col) > 0.0) {
				float v = s.z * sin(TIME * 48.0 + s.w);
				if (s.w >= 200.0) trunk += v * tree_h(s.w, lyy);          // обычный удар по блоку (s.w < 200) наклоняет клетку в vertex(): ничего не обрезается
			}
		}
		vec2 uv2 = UV;
		if (trunk != 0.0) {
			uv2.x = UV.x - trunk * 1.9 / atlas.x;
			float tlo = col * 16.0 / atlas.x + 0.5 / atlas.x; float thi = (col + 1.0) * 16.0 / atlas.x - 0.5 / atlas.x;
			trunk_out = (uv2.x < tlo - 0.5 / atlas.x || uv2.x > thi + 0.5 / atlas.x) ? 0.0 : 1.0;
			uv2.x = clamp(uv2.x, tlo, thi);
		}
		COLOR = texture(TEXTURE, uv2) * vcol * trunk_out;
	}
	float g = td.b;
	if (g > 0.0) {
		float ph = hash(floor(wpos / 16.0)) * 6.2831;
		float pulse = 0.5 + 0.5 * sin(TIME * 2.0 + ph);
		COLOR.rgb *= 1.0 + g * (0.25 + 0.75 * pulse);
		COLOR.rgb += vec3(0.10, 0.09, 0.05) * g * pulse * COLOR.a;
	}
}
