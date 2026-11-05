"""Gera app.ico, icon-256.png e app.icns a partir do desenho de icon.svg.

O desenho (quadrado arredondado violeta #6D4AFF com um play branco) é refeito aqui em coordenadas
do SVG porque o Pillow não lê SVG; mantenha os dois em sincronia. Uso: python gerar-icones.py
"""
from pathlib import Path
from PIL import Image, ImageDraw

AQUI = Path(__file__).parent
BASE = 1024  # supersampling: desenha grande e reduz, para bordas suaves em 16 px


def desenhar() -> Image.Image:
    k = BASE / 256
    img = Image.new("RGBA", (BASE, BASE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, BASE - 1, BASE - 1], radius=56 * k, fill="#6D4AFF")
    d.polygon([(98 * k, 72 * k), (98 * k, 184 * k), (190 * k, 128 * k)], fill="white")
    return img


mestre = desenhar()
mestre.resize((256, 256), Image.LANCZOS).save(AQUI / "icon-256.png")
mestre.save(AQUI / "app.ico", sizes=[(s, s) for s in (16, 24, 32, 48, 64, 128, 256)])
mestre.save(AQUI / "app.icns", sizes=[(s, s) for s in (16, 32, 64, 128, 256, 512, 1024)])
