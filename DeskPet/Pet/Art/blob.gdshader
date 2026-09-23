// Procedural blob body + face. Every parameter is driven by tweens from BlobArt; nothing reads TIME,
// so low-processor mode only redraws when a tween actually changes something.
shader_type canvas_item;

uniform vec4 body_color : source_color = vec4(0.36, 0.78, 0.62, 1.0);
uniform vec4 shade_color : source_color = vec4(0.20, 0.55, 0.47, 1.0);
uniform vec4 ink_color : source_color = vec4(0.12, 0.14, 0.18, 1.0);
uniform vec4 blush_color : source_color = vec4(1.0, 0.55, 0.62, 1.0);
uniform float saturation = 1.0;
uniform float blink = 0.0;      // 0 open, 1 closed
uniform float eye_open = 1.0;   // >1 wide (alert), <1 narrowed
uniform float happy = 0.0;      // 1 turns eyes into ^ ^ arcs
uniform float droop = 0.0;      // heavy lids, sagging head
uniform float smile = 0.3;      // -1 frown .. 1 grin
uniform float blush = 0.0;
uniform vec2 look = vec2(0.0);  // pupil offset, -1..1

float aa(float d) { return clamp(0.5 - d / max(fwidth(d), 1e-4), 0.0, 1.0); }

float body_sdf(vec2 p) {
	vec2 c = vec2(0.5, 0.56);
	vec2 r = vec2(0.45, p.y < c.y ? 0.43 - 0.06 * droop : 0.40);
	vec2 q = (p - c) / r;
	return (length(q) - 1.0) * min(r.x, r.y);
}

float eye_sdf(vec2 p, vec2 c) {
	float r = 0.075 * eye_open;
	float lid = mix(1.0, 0.08, max(blink, droop * 0.55));
	vec2 q = (p - c) / vec2(r, r * lid);
	return (length(q) - 1.0) * r * lid;
}

float arc_sdf(vec2 p, vec2 c, float r, float w) {
	vec2 q = p - c;
	float ring = abs(length(q) - r) - w;
	return q.y > 0.0 ? length(vec2(abs(q.x) - r, q.y)) - w : ring;
}

float mouth_sdf(vec2 p) {
	vec2 c = vec2(0.5 + look.x * 0.02, 0.66);
	float x = clamp((p.x - c.x) / 0.07, -1.0, 1.0);
	// UV y grows downwards, so a smile pushes the middle of the mouth down.
	float curve = c.y + smile * 0.035 * (1.0 - x * x);
	float d = abs(p.y - curve) - 0.011;
	return max(d, abs(p.x - c.x) - 0.07);
}

void fragment() {
	vec2 p = UV;
	float body = aa(body_sdf(p));
	vec3 col = mix(shade_color.rgb, body_color.rgb, smoothstep(0.95, 0.35, p.y));
	col += 0.10 * smoothstep(0.25, 0.0, length(p - vec2(0.36, 0.30)));

	vec2 eye_l = vec2(0.36, 0.47 + droop * 0.02);
	vec2 eye_r = vec2(0.64, 0.47 + droop * 0.02);
	vec2 pupil = look * vec2(0.03, 0.02);
	float eyes_open = aa(min(eye_sdf(p, eye_l + pupil * 0.4), eye_sdf(p, eye_r + pupil * 0.4)));
	float eyes_arc = aa(min(arc_sdf(p, eye_l, 0.05, 0.012), arc_sdf(p, eye_r, 0.05, 0.012)));
	float eyes = mix(eyes_open, eyes_arc, happy);
	// Catchlights sell the gaze; they vanish as the lids close or the eyes turn to happy arcs.
	vec2 glint = vec2(-0.022, -0.024) * eye_open + pupil * 0.6;
	float lids_open = (1.0 - max(blink, droop * 0.55)) * (1.0 - happy);
	float glints = aa(min(length(p - eye_l - glint), length(p - eye_r - glint)) - 0.018 * eye_open) * lids_open * eyes_open;
	float cheeks = smoothstep(0.07, 0.0, min(length(p - vec2(0.27, 0.58)), length(p - vec2(0.73, 0.58))));

	col = mix(col, blush_color.rgb, cheeks * blush * 0.6);
	col = mix(col, ink_color.rgb, max(eyes, aa(mouth_sdf(p))));
	col = mix(col, vec3(1.0), glints * 0.9);
	col = mix(vec3(dot(col, vec3(0.299, 0.587, 0.114))), col, saturation);
	COLOR = vec4(col, body * body_color.a);
}
