// Verre dépoli de l'interface (C13) : l'écran derrière un panneau, flouté par lecture à mip réduit,
// découpé en rectangle arrondi. Le StyleBox du panneau (teinte sapin à 80 %, filet clair, ombre) se dessine par-dessus.
// Coût : cinq lectures de texture par pixel, pas de boucle ; la copie de l'écran est faite une fois par image.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 3.0;
uniform float radius = 16.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float opacity = 1.0;

varying vec2 local;

void vertex() {
	local = VERTEX;
}

void fragment() {
	// Taille du rectangle en unités du canevas, sans uniforme à tenir à jour.
	vec2 size = fwidth(local) / max(fwidth(UV), vec2(1e-6));
	vec2 q = abs(local - size * 0.5) - (size * 0.5 - vec2(radius));
	float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
	float mask = clamp(0.5 - d / max(fwidth(d), 1e-4), 0.0, 1.0);

	vec2 o = SCREEN_PIXEL_SIZE * exp2(blur_lod);
	vec3 blur = textureLod(screen_texture, SCREEN_UV, blur_lod).rgb * 0.4;
	blur += textureLod(screen_texture, SCREEN_UV + vec2(o.x, o.y), blur_lod).rgb * 0.15;
	blur += textureLod(screen_texture, SCREEN_UV + vec2(-o.x, o.y), blur_lod).rgb * 0.15;
	blur += textureLod(screen_texture, SCREEN_UV + vec2(o.x, -o.y), blur_lod).rgb * 0.15;
	blur += textureLod(screen_texture, SCREEN_UV + vec2(-o.x, -o.y), blur_lod).rgb * 0.15;

	COLOR = vec4(blur * tint.rgb, mask * opacity * tint.a);
}
