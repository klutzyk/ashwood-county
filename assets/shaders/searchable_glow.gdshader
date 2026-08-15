shader_type canvas_item;
render_mode blend_add;

// Soft neutral glow for searchable objects. The glow is derived from the
// object's own silhouette, so it hugs the actual artwork instead of drawing a
// ring around it. Intensity is driven entirely by the glow_strength uniform.
uniform sampler2D glow_tex : source_color;
uniform float glow_strength : hint_range(0.0, 1.5) = 0.0;
uniform vec4 glow_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	float silhouette = texture(glow_tex, UV).a;
	float halo = 0.0;
	for (int y = -3; y <= 3; y++) {
		for (int x = -3; x <= 3; x++) {
			if (x == 0 && y == 0) continue;
			halo += texture(glow_tex, UV + vec2(float(x), float(y)) * 0.013).a;
		}
	}
	halo *= 1.0 / 48.0;
	float glow = (halo * 1.0 + silhouette * 0.28) * glow_strength;
	COLOR = vec4(glow_color.rgb * glow, 1.0);
}
