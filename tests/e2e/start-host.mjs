import { spawn } from 'node:child_process';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const state = process.env.MEDICAL_E2E_STATE;
if (!state) throw new Error('Run npm test so the fixture database is isolated.');
const kind = process.argv[2];
const project = { api: 'WebAPI', staff: 'BlazorServer', patient: 'BlazorWasm' }[kind];
const port = { api: 5031, staff: 5187, patient: 5016 }[kind];
if (!project) throw new Error('Unknown host.');
const env = {
  ...process.env, ASPNETCORE_ENVIRONMENT: 'Development',
  ConnectionStrings__MedicalBookingSystem: `Data Source=${join(state, 'medical.db')};Pooling=False;Default Timeout=10`,
  JwtSettings__SecretKey: 'browser-tests-only-random-independent-signing-key-at-least-32-bytes',
  Database__InitializeOnStartup: 'true', Seed__Enabled: 'true',
  Seed__PatientPassword: 'DemoPatient2026!', Seed__StaffPassword: 'DemoStaff2026!',
  Media__PublicBaseUrl: 'http://localhost:5187', Media__StoragePath: join(state, 'photos'),
  DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1'
};
const child = spawn(process.env.MEDICAL_DOTNET ?? 'dotnet', ['run', '--project', resolve(root, `MedicalBookingSystem/MedicalBookingSystem.${project}`),
  '--configuration', 'Release', '--no-build', '--no-restore', '--no-launch-profile', '--urls', `http://localhost:${port}`], { cwd: root, env, stdio: 'inherit' });
for (const signal of ['SIGTERM', 'SIGINT']) process.on(signal, () => child.kill(signal));
child.on('exit', code => { process.exitCode = code ?? 1; });
