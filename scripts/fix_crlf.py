# Copyright (c) Zenix. All rights reserved.
# Licensed under the proprietary and confidential license.

import os

for root, _, files in os.walk('.'):
    if any(p in root for p in ['.git', 'bin', 'obj', '.vs', '.agents', '.specify']):
        continue
    for file in files:
        if file.endswith(('.cs', '.resx', '.csproj', '.manifest', '.sln')):
            path = os.path.join(root, file)
            with open(path, 'rb') as f:
                data = f.read()
            if not data:
                continue
            normalized = data.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
            with open(path, 'wb') as f:
                f.write(normalized)

print("CRLF normalization complete.")
