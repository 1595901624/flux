"""Regenerate Windows icon assets from Assets/app.svg.

Requires: python -m pip install resvg-py Pillow
Run: python scripts/generate-icons.py
"""

from io import BytesIO
from pathlib import Path

from PIL import Image
from resvg_py import svg_to_bytes


ASSETS = Path(__file__).resolve().parents[1] / "Assets"


def render(size: int) -> Image.Image:
    png = svg_to_bytes(svg_path=str(ASSETS / "app.svg"), width=size, height=size)
    return Image.open(BytesIO(png)).convert("RGBA")


def main() -> None:
    for filename, size in {
        "Square44x44Logo.png": 44,
        "Square150x150Logo.png": 150,
        "StoreLogo.png": 50,
    }.items():
        render(size * 4).resize((size, size), Image.Resampling.LANCZOS).save(ASSETS / filename)

    for filename, dimensions, icon_size in [
        ("Wide310x310Logo.png", (310, 150), 120),
        ("SplashScreen.png", (620, 300), 192),
    ]:
        canvas = Image.new("RGBA", dimensions)
        icon = render(icon_size * 4).resize((icon_size, icon_size), Image.Resampling.LANCZOS)
        canvas.alpha_composite(icon, ((dimensions[0] - icon_size) // 2, (dimensions[1] - icon_size) // 2))
        canvas.save(ASSETS / filename)

    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    render(1024).save(ASSETS / "app.ico", sizes=[(size, size) for size in sizes])
    print("Regenerated ICO and five PNG assets from Assets/app.svg")


if __name__ == "__main__":
    main()
