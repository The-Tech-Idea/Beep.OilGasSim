/* Run TypeScript compiler with a real Node.js binary on Windows. */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const projectRoot = path.join(__dirname, '..');
const tscEntry = path.join(projectRoot, 'node_modules', 'typescript', 'bin', 'tsc');

function resolveNodeExecutable() {
  if (process.env.NODE_EXE && fs.existsSync(process.env.NODE_EXE)) {
    return process.env.NODE_EXE;
  }

  if (process.platform === 'win32') {
    const programFilesNode = 'C:\\Program Files\\nodejs\\node.exe';
    if (fs.existsSync(programFilesNode)) {
      return programFilesNode;
    }

    try {
      const lines = execSync('where.exe node.exe', { encoding: 'utf8' })
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean)
        .filter(
          (line) =>
            !line.toLowerCase().includes('appdata\\roaming\\npm') &&
            !line.toLowerCase().includes('cursor\\resources'),
        );

      if (lines[0] && fs.existsSync(lines[0])) {
        return lines[0];
      }
    } catch {
      // fall through
    }
  }

  return process.execPath;
}

if (!fs.existsSync(tscEntry)) {
  console.error('TypeScript not found. Run: npm install');
  process.exit(1);
}

const nodeExe = resolveNodeExecutable();
const args = [tscEntry, ...process.argv.slice(2)];

const child = spawn(nodeExe, args, {
  cwd: projectRoot,
  stdio: 'inherit',
  shell: false,
});

child.on('error', (err) => {
  console.error('Failed to start tsc:', err.message);
  process.exit(1);
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }
  process.exit(code ?? 1);
});
