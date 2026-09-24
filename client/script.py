import os

# Directories to completely skip
IGNORE_DIRS = {
    'node_modules',
    '.git',
    '.idea',
    '__pycache__',
    # 'dist',
    # 'build',
    # 'locales',
    # 'commands',
    # 'flavibot',
    '.import',
    '.godot',
    "node_modules",
    ".git",
    "dist",
    "build",
    ".next",
    ".cache",
    "target",
    "venv",
    "frontend",
    # "backend",
    "ai-service",
    ".github",
    ".idea",
    ".vscode",
    ".claude",
    "geo",
    "geodata",
    "bin",
    "obj",
    "Migrations"
}

# File extensions to skip
IGNORE_EXTENSIONS = {
    '.exe', '.dll', '.jar', '.class',
    '.zip', '.7z', '.rar',
    '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico', '.hdr', '.svg',
    '.mp3', '.wav', '.ogg',
    '.mp4', '.mkv', '.avi',
    '.ttf', '.otf',
    '.glb', '.import',
    '.texarr', '.exr', '.stex', '.lmbake', '.sample', '.oggstr', '.scn', '.translation', '.md5', '.mp3str'
    '.lock', 
    '.pdf', 
    # '.gitignore',
    '.log'
}

# Specific filenames to skip
IGNORE_FILENAMES = {
    'package-lock.json',
    'yarn.lock',
    'pnpm-lock.yaml',
    'bug_report.yaml',
    'feature_request.yaml',
    'FUNDING.yml',
    'LICENSE',
    # 'SECURITY.md',
    # 'Translation.md',
    # '.gitignore',
    'script.py',
    # 'snowbringer.txt',
    # 'snowbringer.db',
    # 'LavaLogo.txt',
    'folder_structure.txt',
    'Disaster-Management.txt',
    'CLAUDE.md',
    'requested_summary.json',
    'full_read_coverage.json',
    '404error.json'
}


def compile_directory_to_txt(start_dir, output_file='dircontents.txt'):
    """
    Recursively traverses the given directory, collects absolute paths and contents of all files,
    and writes them to a single text file in a format that's easy for an LLM to parse.
    """
    start_dir = os.path.abspath(start_dir)

    with open(output_file, 'w', encoding='utf-8') as out_f:
 
        tree_path = os.path.join(start_dir, 'folder_structure.txt')

        out_f.write(f'the directory structure:\n')
        out_f.write(f'\n')

        try:
            with open(tree_path, 'r', encoding='utf-8', errors='ignore') as f:
                out_f.write(f.read())
        except Exception as e:
            out_f.write(f'[Error reading file: {e}]')

        out_f.write('\n\nthe file contents of some files:\n\n')

        for root, dirs, files in os.walk(start_dir):

            # Remove ignored directories in-place (important)
            dirs[:] = [d for d in dirs if d not in IGNORE_DIRS]

            for file_name in files:
                if file_name in IGNORE_FILENAMES:
                    continue

                file_path = os.path.join(root, file_name)
                ext = os.path.splitext(file_name)[1].lower()

                if ext in IGNORE_EXTENSIONS:
                    continue

                file_path = os.path.abspath(file_path)

                out_f.write('---START FILE---\n')
                out_f.write(f'PATH: {file_path}\n')
                out_f.write('CONTENT:\n')

                try:
                    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                        out_f.write(f.read())
                except Exception as e:
                    out_f.write(f'[Error reading file: {e}]')

                out_f.write('\n---END FILE---\n\n')


if __name__ == '__main__':
    cwdir = os.getcwd()
    filename = os.path.basename(cwdir)

    compile_directory_to_txt(cwdir, output_file=f'{filename}.txt')
    print(f"Compiled contents to '{filename}.txt'")