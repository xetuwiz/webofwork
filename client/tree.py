import json
from pathlib import Path

EXCLUDE_DIRS = {
    "node_modules",
    ".git",
    "dist",
    "build",
    ".next",
    ".cache",
    "ref",
    "bin",
    "obj"
}

MAX_DEPTH = None  # set to int (e.g. 5) or None for unlimited
OUTPUT_FILE = "folder_structure.txt"


def build_tree(path: Path, depth: int = 0, max_depth=None):
    if max_depth is not None and depth > max_depth:
        return None

    if path.is_dir():
        if path.name in EXCLUDE_DIRS:
            return {
                "type": "directory",
                "name": path.name,
                "excluded": True
            }

        node = {
            "type": "directory",
            "name": path.name,
            "children": []
        }

        try:
            entries = sorted(
                path.iterdir(),
                key=lambda p: (p.is_file(), p.name.lower())
            )
        except PermissionError:
            return None

        for entry in entries:
            child = build_tree(entry, depth + 1, max_depth)
            if child is not None:
                node["children"].append(child)

        return node

    return {
        "type": "file",
        "name": path.name
    }


if __name__ == "__main__":
    root_path = Path(".").resolve()

    tree = {
        "type": "directory",
        "name": root_path.name,
        "root": True,
        "children": []
    }

    for item in sorted(
        root_path.iterdir(),
        key=lambda p: (p.is_file(), p.name.lower())
    ):
        node = build_tree(item, depth=1, max_depth=MAX_DEPTH)
        if node is not None:
            tree["children"].append(node)

    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(tree, f, indent=2)

    print(f"Folder structure written to {OUTPUT_FILE}")
