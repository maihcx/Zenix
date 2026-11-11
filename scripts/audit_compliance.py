# Copyright (c) Zenix. All rights reserved.
# Licensed under the proprietary and confidential license.

import os

cs_header = "// Copyright (c) Zenix. All rights reserved."

errors = []
for root, _, files in os.walk("."):
    if any(p in root for p in [".git", "bin", "obj", ".vs", ".agents", ".specify"]):
        continue
    for file in files:
        path = os.path.join(root, file)
        ext = os.path.splitext(file)[1].lower()
        if ext in [".cs", ".resx", ".csproj", ".manifest", ".sln"]:
            data = open(path, "rb").read()
            # Check BOM
            if data.startswith(b"\xef\xbb\xbf"):
                errors.append(f"{path}: Has UTF-8 BOM!")
            # Check CRLF
            cr = data.count(b"\r")
            lf = data.count(b"\n")
            if cr != lf or cr == 0:
                errors.append(f"{path}: Line ending mismatch or no CRLF: CR={cr}, LF={lf}!")
            
            # For C# files
            if ext == ".cs":
                text = data.decode("utf-8")
                lines = text.split("\r\n")
                if not text.startswith(cs_header):
                    errors.append(f"{path}: Missing required copyright header!")
                # Check using placement
                namespace_found = False
                for idx, line in enumerate(lines):
                    trimmed = line.strip()
                    if trimmed.startswith("namespace "):
                        namespace_found = True
                    if namespace_found and trimmed.startswith("using ") and not trimmed.startswith("using var") and not trimmed.startswith("using ("):
                        errors.append(f"{path}:{idx+1}: Using directive inside or below namespace (IDE0065 violation)!")
                # Check UI types
                for ui_token in ["Microsoft.UI.Xaml", "UIElement", "FrameworkElement", "Window "]:
                    if ui_token in text:
                        errors.append(f"{path}: Contains prohibited UI type token {ui_token}!")

if not errors:
    print("AUDIT PASSED: All files 100% compliant with CRLF, UTF-8 no BOM, IDE0065, IDE0073, and Zero UI types!")
else:
    for e in errors:
        print("AUDIT FAILURE:", e)
