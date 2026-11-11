import os
import sys

def convert_to_crlf_and_strip_bom(file_path):
    with open(file_path, "rb") as f:
        data = f.read()
    
    modified = False
    # Strip UTF-8 BOM if present
    if data.startswith(b"\xef\xbb\xbf"):
        data = data[3:]
        modified = True
        
    # Replace any existing CRLF with LF, then all LF with CRLF
    normalized = data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    if normalized != data or modified:
        with open(file_path, "wb") as f:
            f.write(normalized)
    cr = normalized.count(b"\r")
    lf = normalized.count(b"\n")
    print(f"{file_path}: CR={cr}, LF={lf}, CRLF_clean={cr == lf}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        targets = sys.argv[1:]
    else:
        targets = []
        for root, _, files in os.walk("."):
            if any(p in root for p in [".git", "bin", "obj", ".vs", ".agents"]):
                continue
            for file in files:
                if file.endswith((".cs", ".csproj", ".manifest", ".sln", ".resx", ".xml", ".md", ".py")):
                    targets.append(os.path.join(root, file))
    for t in targets:
        convert_to_crlf_and_strip_bom(t)
