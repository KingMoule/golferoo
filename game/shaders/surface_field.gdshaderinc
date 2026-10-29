// Champ des surfaces, partagé par le terrain et l'eau : ils tracent ainsi la même rive.
// Deux textures RGBA d'un texel par cellule : poids « un parmi huit » par surface (A : départ, green, fairway, rough ;
// B : sable, eau, bois, chemin). Les poids, flous sur une tuile de 8 m et légèrement bruités, forment un champ continu ;
// la frontière entre deux surfaces est la ligne où leurs poids s'égalent, d'où des contours lisses sans marches de cellule.
uniform sampler2D weights_a : filter_linear, repeat_disable;
uniform sampler2D weights_b : filter_linear, repeat_disable;
uniform vec2 grid_size = vec2(384.0, 128.0);
// Rayon du flou, en mètres : de l'ordre d'une tuile, pour effacer l'escalier des tuiles de 8 m.
uniform float blur_meters = 8.0;
// Bruit doux à grande échelle : des bords un peu irréguliers, pas des dents.
uniform float noise_amount = 0.12;
uniform float noise_scale = 0.08;

float field_hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(field_hash(i), field_hash(i + vec2(1.0, 0.0)), f.x),
			mix(field_hash(i + vec2(0.0, 1.0)), field_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Poids flous et bruités des huit surfaces au point p (mètres) : flou en deux couronnes (8 + 8 prises) autour du centre.
void surface_field(vec2 p, out vec4 a, out vec4 b) {
	vec2 uv = p / grid_size;
	a = texture(weights_a, uv) * 2.0;
	b = texture(weights_b, uv) * 2.0;
	float total = 2.0;
	for (int k = 0; k < 8; k++) {
		float ang = float(k) * 0.785398;
		vec2 o1 = vec2(cos(ang), sin(ang)) * (blur_meters * 0.5) / grid_size;
		vec2 o2 = vec2(cos(ang + 0.392699), sin(ang + 0.392699)) * blur_meters / grid_size;
		a += texture(weights_a, uv + o1) + 0.6 * texture(weights_a, uv + o2);
		b += texture(weights_b, uv + o1) + 0.6 * texture(weights_b, uv + o2);
		total += 1.6;
	}
	vec2 q = p * noise_scale;
	a = a / total + (vec4(vnoise(q), vnoise(q + vec2(17.3, 5.1)), vnoise(q + vec2(34.6, 10.2)), vnoise(q + vec2(51.9, 15.3))) - 0.5) * noise_amount;
	b = b / total + (vec4(vnoise(q + vec2(69.2, 20.4)), vnoise(q + vec2(86.5, 25.5)), vnoise(q + vec2(103.8, 30.6)), vnoise(q + vec2(121.1, 35.7))) - 0.5) * noise_amount;
}

// Surface gagnante, sa suivante et la distance à leur frontière en mètres (la pente du champ vaut environ 2 / flou).
void surface_pick(vec2 p, out int best, out int second, out float dist) {
	vec4 a;
	vec4 b;
	surface_field(p, a, b);
	float w[8];
	w[0] = a.r; w[1] = a.g; w[2] = a.b; w[3] = a.a;
	w[4] = b.r; w[5] = b.g; w[6] = b.b; w[7] = b.a;
	best = 0;
	second = 1;
	float v1 = -1.0;
	float v2 = -1.0;
	for (int i = 0; i < 8; i++) {
		if (w[i] > v1) {
			v2 = v1;
			second = best;
			v1 = w[i];
			best = i;
		} else if (w[i] > v2) {
			v2 = w[i];
			second = i;
		}
	}
	dist = (v1 - v2) * blur_meters * 0.5;
}
