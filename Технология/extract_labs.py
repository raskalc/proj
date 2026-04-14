from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET

NS = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}


def read_docx(path: Path) -> str:
    with zipfile.ZipFile(path) as zf:
        data = zf.read("word/document.xml")
    root = ET.fromstring(data)
    parts = []
    for para in root.findall(".//w:p", NS):
        texts = [node.text for node in para.findall(".//w:t", NS) if node.text]
        if texts:
            parts.append("".join(texts))
    return "\n".join(parts)


lines = []
for p in sorted(Path(".").rglob("*.docx")):
    lines.append(f"--- {p} ---")
    try:
        lines.append(read_docx(p))
    except Exception as exc:
        lines.append(f"ERROR: {exc}")
    lines.append("")

Path("labs_text_dump.txt").write_text("\n".join(lines), encoding="utf-8")
