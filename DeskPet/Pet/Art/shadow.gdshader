shader_type canvas_item;

uniform float strength = 0.28;

void fragment() {
	float d = length((UV - 0.5) * 2.0);
	COLOR = vec4(0.0, 0.0, 0.0, strength * smoothstep(1.0, 0.2, d));
}
