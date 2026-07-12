// Realistic authenticated traffic mix through the gateway — exercises JWT validation,
// per-tenant DB connection resolution, Redis cache-aside reads, and pagination across
// Identity, Category, Translation, FileManager, all scoped to a real tenant.
// setup() logs in once with an existing account and its token (+ tenant header) is
// reused by every VU so auth cost doesn't pollute the throughput measurement.
//
// Usage:
//   k6 run LoadTests/k6/authenticated-flow.js
//   k6 run -e PEAK_RATE=1000 LoadTests/k6/authenticated-flow.js
//   k6 run -e TENANT_ID=other-tenant -e LOGIN_EMAIL=x@y.com -e LOGIN_PASSWORD=pass LoadTests/k6/authenticated-flow.js
import http from 'k6/http';
import { check, fail } from 'k6';

const BASE = __ENV.GATEWAY_URL || 'http://localhost:5000';
const PEAK_RATE = parseInt(__ENV.PEAK_RATE || '800', 10);
const QUICK = __ENV.QUICK === '1';
const TENANT_ID = __ENV.TENANT_ID || 'ihsandev';
const LOGIN_EMAIL = __ENV.LOGIN_EMAIL || 'ihsandev@ihsandev.com';
const LOGIN_PASSWORD = __ENV.LOGIN_PASSWORD || '@Test123';
const REGISTER_PASSWORD = 'LoadTest123!';

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-arrival-rate',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 150,
      maxVUs: 1500,
      stages: QUICK
        ? [
            { target: Math.round(PEAK_RATE * 0.4), duration: '10s' },
            { target: PEAK_RATE, duration: '15s' },
            { target: PEAK_RATE, duration: '10s' },
            { target: 0, duration: '5s' },
          ]
        : [
            { target: Math.round(PEAK_RATE * 0.15), duration: '30s' },
            { target: Math.round(PEAK_RATE * 0.5), duration: '1m' },
            { target: PEAK_RATE, duration: '2m' },
            { target: PEAK_RATE, duration: '1m' },
            { target: 0, duration: '30s' },
          ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800'],
  },
};

function extractToken(res) {
  if (res.status < 200 || res.status >= 300 || !res.body) return null;
  let body;
  try {
    body = res.json();
  } catch {
    return null; // non-JSON body (e.g. 429 from the gateway's per-IP rate limiter)
  }
  return body && (body.accessToken || body.AccessToken || body.token || body.Token);
}

export function setup() {
  const tenantHeaders = { 'Content-Type': 'application/json', 'x-tenant-id': TENANT_ID };

  const loginRes = http.post(
    `${BASE}/api/v1/auth/login`,
    JSON.stringify({ email: LOGIN_EMAIL, password: LOGIN_PASSWORD }),
    { headers: tenantHeaders }
  );

  let token = extractToken(loginRes);

  if (!token) {
    // Fall back to a throwaway registration under the same tenant if the given
    // account doesn't exist in this environment's database.
    const email = `loadtest_${Date.now()}_${Math.floor(Math.random() * 1e6)}@example.com`;
    const registerRes = http.post(
      `${BASE}/api/v1/auth/register`,
      JSON.stringify({ email, password: REGISTER_PASSWORD, firstName: 'Load', lastName: 'Test' }),
      { headers: tenantHeaders }
    );
    token = extractToken(registerRes);
    if (!token) {
      fail(
        `setup: could not obtain a token — login(${loginRes.status}): ${loginRes.body} register(${registerRes.status}): ${registerRes.body}`
      );
    }
  }

  return { token };
}

function logFailure(name, res, headers) {
  console.log(
    `${name} failure: status=${res.status} error=${res.error} error_code=${res.error_code} ` +
    `duration=${res.timings.duration.toFixed(0)}ms auth_len=${(headers.Authorization || '').length} ` +
    `www_auth=${res.headers && res.headers['Www-Authenticate']} body=${(res.body || '').slice(0, 200)}`
  );
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}`, 'x-tenant-id': TENANT_ID };
  const roll = Math.random();

  if (roll < 0.25) {
    const res = http.get(`${BASE}/api/v1/user/profile`, { headers, tags: { name: 'identity_profile' } });
    const ok = check(res, { 'profile 200': (r) => r.status === 200 });
    if (!ok) logFailure('profile', res, headers);
  } else if (roll < 0.5) {
    const res = http.get(`${BASE}/api/v1/categories/?page=1&pageSize=20`, {
      headers,
      tags: { name: 'category_list' },
    });
    const ok = check(res, { 'categories 200': (r) => r.status === 200 });
    if (!ok) logFailure('categories', res, headers);
  } else if (roll < 0.75) {
    const res = http.get(`${BASE}/api/v1/translations/en`, { headers, tags: { name: 'translation_get' } });
    const ok = check(res, { 'translations 200': (r) => r.status === 200 });
    if (!ok) logFailure('translations', res, headers);
  } else {
    const res = http.get(`${BASE}/api/v1/filemanager/files?PageNumber=1&PageSize=20`, {
      headers,
      tags: { name: 'filemanager_list' },
    });
    const ok = check(res, { 'filemanager 200': (r) => r.status === 200 });
    if (!ok) logFailure('filemanager', res, headers);
  }
}
