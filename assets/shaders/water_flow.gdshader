shader_type canvas_item;
render_mode unshaded;

uniform vec4 deep_color : source_color = vec4(0.10, 0.25, 0.28, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.23, 0.48, 0.50, 1.0);
uniform vec4 highlight_color : source_color = vec4(0.52, 0.72, 0.70, 1.0);
uniform vec2 flow_direction = vec2(0.7, 0.25);
uniform float flow_speed : hint_range(0.0, 0.2) = 0.035;
uniform float ripple_scale : hint_range(2.0, 30.0) = 8.0;
uniform float ripple_strength : hint_range(0.0, 0.2) = 0.02;
uniform float highlight_strength : hint_range(0.0, 0.5) = 0.08;
uniform float opacity : hint_range(0.0, 1.0) = 0.95;
varying vec2 water_position;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0)), f.x), f.y);
}

void vertex() {
    // Every water polygon is authored in the shared projected county canvas,
    // so this remains stable as the camera moves and zooms.
    water_position = VERTEX / 320.0;
}

void fragment() {
    vec2 direction = normalize(flow_direction + vec2(0.0001));
    vec2 moving_uv = water_position * ripple_scale + direction * TIME * flow_speed * ripple_scale;
    float broad = value_noise(moving_uv);
    float fine = value_noise(moving_uv * 1.9 - direction * TIME * flow_speed * 4.0);
    float ripple = mix(broad, fine, 0.38);
    float glint = smoothstep(0.84, 0.98, fine + sin((water_position.x + water_position.y) * 18.0 + TIME * flow_speed * 12.0) * ripple_strength);
    vec4 water = mix(deep_color, shallow_color, ripple * 0.18 + 0.10);
    water = mix(water, highlight_color, glint * highlight_strength);
    water.a *= opacity;
    COLOR = water * COLOR;
}
