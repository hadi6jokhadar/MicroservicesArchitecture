// Anonymous, no-auth stress test — measures raw infra ceiling (gateway + Kestrel + ThreadPool)
// without touching business logic or the database-per-tenant path.
//
// Usage:
//   k6 run LoadTests/k6/health-baseline.js
//   k6 run -e MODE=direct LoadTests/k6/health-baseline.js      (bypasses gateway, isolates its overhead)
//   k6 run -e MODE=aggregate LoadTests/k6/health-baseline.js   (hits /health/aggregate — an 8x fan-out per
//                                                                request; use a MUCH lower PEAK_RATE, e.g. 20-50)
//   k6 run -e PEAK_RATE=5000 LoadTests/k6/health-baseline.js
import http from 'k6/http';
import { check } from 'k6';

const GATEWAY = __ENV.GATEWAY_URL || 'http://localhost:5000';
const MODE = __ENV.MODE || 'gateway'; // 'gateway' | 'direct' | 'aggregate'
const PEAK_RATE = parseInt(__ENV.PEAK_RATE || '2000', 10);
const QUICK = __ENV.QUICK === '1';

const DIRECT = {
  identity: __ENV.IDENTITY_URL || 'http://localhost:5001',
  tenant: __ENV.TENANT_URL || 'http://localhost:5002',
  notification: __ENV.NOTIFICATION_URL || 'http://localhost:5004',
  filemanager: __ENV.FILEMANAGER_URL || 'http://localhost:5005',
  translation: __ENV.TRANSLATION_URL || 'http://localhost:5006',
  category: __ENV.CATEGORY_URL || 'http://localhost:5007',
  ai: __ENV.AI_URL || 'http://localhost:5008',
  nasheed: __ENV.NASHEED_URL || 'http://localhost:5009',
};
const DIRECT_NAMES = Object.keys(DIRECT);

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 300,
      maxVUs: 3000,
      stages: QUICK
        ? [
            { target: Math.round(PEAK_RATE * 0.4), duration: '10s' },
            { target: PEAK_RATE, duration: '15s' },
            { target: PEAK_RATE, duration: '10s' },
            { target: 0, duration: '5s' },
          ]
        : [
            { target: Math.round(PEAK_RATE * 0.1), duration: '30s' },
            { target: Math.round(PEAK_RATE * 0.4), duration: '1m' },
            { target: PEAK_RATE, duration: '2m' },
            { target: PEAK_RATE, duration: '1m' },
            { target: 0, duration: '30s' },
          ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  if (MODE === 'direct') {
    const name = DIRECT_NAMES[Math.floor(Math.random() * DIRECT_NAMES.length)];
    const res = http.get(`${DIRECT[name]}/health`, { tags: { name: `${name}_health` } });
    check(res, { [`${name} /health 200`]: (r) => r.status === 200 });
    return;
  }

  if (MODE === 'aggregate') {
    const res = http.get(`${GATEWAY}/health/aggregate`, { tags: { name: 'gateway_health_aggregate' } });
    check(res, { 'gateway /health/aggregate 200': (r) => r.status === 200 });
    return;
  }

  const res = http.get(`${GATEWAY}/health`, { tags: { name: 'gateway_health' } });
  check(res, { 'gateway /health 200': (r) => r.status === 200 });
}
