#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Конвертация WebP -> PNG для образцов тёмного UI/UX.
Использование:
  python scripts/convert_webp_to_png.py
  python scripts/convert_webp_to_png.py "I:\SAMPLES OF DARK UI UX II"
  python scripts/convert_webp_to_png.py "I:\path\to\folder" --output "I:\path\to\output"
"""

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Установите Pillow: pip install Pillow")
    sys.exit(1)


def convert_webp_to_png(
    source_dir: str | Path,
    output_dir: str | Path | None = None,
    overwrite: bool = False,
) -> int:
    """Конвертирует все .webp в папке в .png. Возвращает количество сконвертированных."""
    source = Path(source_dir)
    if not source.is_dir():
        print(f"Ошибка: папка не найдена: {source}")
        return 0

    out = Path(output_dir) if output_dir else source
    out.mkdir(parents=True, exist_ok=True)

    count = 0
    for fp in source.glob("*.webp"):
        png_path = out / (fp.stem + ".png")
        if png_path.exists() and not overwrite:
            continue
        try:
            img = Image.open(fp)
            img.save(png_path, "PNG")
            count += 1
            print(f"  {fp.name} -> {png_path.name}")
        except Exception as e:
            print(f"  Ошибка {fp.name}: {e}")

    return count


def main():
    parser = argparse.ArgumentParser(description="WebP → PNG конвертер")
    parser.add_argument(
        "source",
        nargs="?",
        default=r"I:\SAMPLES OF DARK UI UX II",
        help="Папка с .webp файлами",
    )
    parser.add_argument(
        "-o", "--output",
        help="Папка для .png (по умолчанию — та же, что source)",
    )
    parser.add_argument(
        "-f", "--overwrite",
        action="store_true",
        help="Перезаписывать существующие .png",
    )
    args = parser.parse_args()

    print(f"Источник: {args.source}")
    if args.output:
        print(f"Вывод:    {args.output}")
    print("---")

    n = convert_webp_to_png(args.source, args.output, args.overwrite)
    print(f"---\nСконвертировано: {n} файлов")


if __name__ == "__main__":
    main()
