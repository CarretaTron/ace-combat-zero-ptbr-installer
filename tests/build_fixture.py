"""Build isolated C# installer tests with tiny embedded resources; no game access."""
from pathlib import Path
import hashlib
import json
import subprocess

HERE = Path(__file__).resolve().parent
SOURCE = HERE.parent / 'src' / 'InstallerEngine.cs'
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
DATA = {
    'exe': b'ACZERO TEST EXECUTABLE\r\n',
    'original_a': b'Original A fixture archive version zero\r\n',
    'original_d': b'Original D fixture archive version zero\r\n',
    'previous_a': b'Previous A known fixture archive version one\r\n',
    'previous_d': b'Previous D known fixture archive version one\r\n',
    'target_a': b'Traducao A fixture archive version eight\r\n',
    'target_d': b'Traducao D fixture archive version eight\r\n',
}

def digest(data):
    return hashlib.sha256(data).hexdigest()

def main():
    fixture = HERE / 'fixture'
    fixture.mkdir(exist_ok=True)
    for name, data in DATA.items():
        (fixture / (name + '.bin')).write_bytes(data)
    manifest = {
        'FormatVersion': 1,
        'Version': 'fixture_008',
        'Files': [
            {'Name': name + '.arc', 'ResourceName': 'AC0_PTBR.' + name + '.arc',
             'Sha256': digest(DATA['target_' + name]), 'Length': len(DATA['target_' + name])}
            for name in ('a', 'd')
        ],
        'AllowedStates': [
            {'Name': prefix, 'ASha256': digest(DATA[prefix + '_a']), 'DSha256': digest(DATA[prefix + '_d'])}
            for prefix in ('original', 'previous', 'target')
        ],
    }
    manifest_path = fixture / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    variants = {'normal': DATA['target_d'], 'corrupt': b'X' + DATA['target_d'][1:], 'truncated': DATA['target_d'][:-1]}
    for variant, payload_d in variants.items():
        path_d = fixture / ('payload_d_' + variant + '.bin')
        path_d.write_bytes(payload_d)
        output = HERE / ('IntegrationTests_' + variant + '.exe')
        command = [str(CSC), '/nologo', '/codepage:65001', '/define:TESTING', '/target:exe',
                   '/r:System.Web.Extensions.dll', '/out:' + str(output),
                   '/resource:' + str(manifest_path) + ',AC0_PTBR.manifest.json',
                   '/resource:' + str(fixture / 'target_a.bin') + ',AC0_PTBR.a.arc',
                   '/resource:' + str(path_d) + ',AC0_PTBR.d.arc',
                   str(SOURCE), str(HERE / 'IntegrationTests.cs')]
        subprocess.run(command, check=True)
        subprocess.run([str(output), variant], check=True)

if __name__ == '__main__':
    main()
