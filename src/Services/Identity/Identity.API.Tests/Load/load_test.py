import requests
import time
import statistics
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime
import urllib3

# Disable SSL warnings for self-signed certificates
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# Configuration
URL = "http://localhost:5001/api/auth/login"
HEADERS = {
    "Content-Type": "application/json",
    "x-tenant-id": "ihsandev"
}
BODY = {
    "email": "ihsandev@gmail.com",
    "password": "@Test123"
}

# Load test parameters
NUM_REQUESTS = 100  # Total number of requests
CONCURRENT_USERS = 10  # Number of concurrent users

def send_request(request_num):
    """Send a single request and return response time and status"""
    start_time = time.time()
    try:
        response = requests.post(URL, json=BODY, headers=HEADERS, timeout=30, verify=False)
        end_time = time.time()
        response_time = (end_time - start_time) * 1000  # Convert to milliseconds
        
        return {
            'request_num': request_num,
            'status_code': response.status_code,
            'response_time': response_time,
            'success': response.status_code == 200
        }
    except Exception as e:
        end_time = time.time()
        response_time = (end_time - start_time) * 1000
        return {
            'request_num': request_num,
            'status_code': 0,
            'response_time': response_time,
            'success': False,
            'error': str(e)
        }

def run_load_test():
    """Execute load test and display results"""
    print(f"\n{'='*60}")
    print(f"LOAD TEST STARTED")
    print(f"{'='*60}")
    print(f"URL: {URL}")
    print(f"Total Requests: {NUM_REQUESTS}")
    print(f"Concurrent Users: {CONCURRENT_USERS}")
    print(f"Started at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'='*60}\n")
    
    results = []
    start_time = time.time()
    
    # Execute requests concurrently
    with ThreadPoolExecutor(max_workers=CONCURRENT_USERS) as executor:
        futures = [executor.submit(send_request, i+1) for i in range(NUM_REQUESTS)]
        
        for future in as_completed(futures):
            result = future.result()
            results.append(result)
            
            # Progress indicator
            if len(results) % 10 == 0:
                print(f"Progress: {len(results)}/{NUM_REQUESTS} requests completed")
    
    end_time = time.time()
    total_duration = end_time - start_time
    
    # Calculate statistics
    response_times = [r['response_time'] for r in results]
    successful_requests = [r for r in results if r['success']]
    failed_requests = [r for r in results if not r['success']]
    
    # Display results
    print(f"\n{'='*60}")
    print(f"LOAD TEST RESULTS")
    print(f"{'='*60}")
    print(f"Total Duration: {total_duration:.2f} seconds")
    print(f"Total Requests: {NUM_REQUESTS}")
    print(f"Successful: {len(successful_requests)} ({len(successful_requests)/NUM_REQUESTS*100:.1f}%)")
    print(f"Failed: {len(failed_requests)} ({len(failed_requests)/NUM_REQUESTS*100:.1f}%)")
    print(f"\n{'='*60}")
    print(f"BACKEND RESPONSE TIME STATISTICS")
    print(f"{'='*60}")
    print(f"Average Response Time: {statistics.mean(response_times):.2f} ms")
    print(f"Median Response Time: {statistics.median(response_times):.2f} ms")
    print(f"Min Response Time: {min(response_times):.2f} ms")
    print(f"Max Response Time: {max(response_times):.2f} ms")
    print(f"Std Deviation: {statistics.stdev(response_times):.2f} ms")
    print(f"\nRequests per Second: {NUM_REQUESTS/total_duration:.2f}")
    print(f"{'='*60}\n")
    
    # Status code distribution
    status_codes = {}
    for r in results:
        status_codes[r['status_code']] = status_codes.get(r['status_code'], 0) + 1
    
    print("Status Code Distribution:")
    for code, count in sorted(status_codes.items()):
        print(f"  {code}: {count} requests")
    
    # Show errors if any
    if failed_requests:
        print(f"\n{'='*60}")
        print("ERRORS:")
        print(f"{'='*60}")
        for req in failed_requests[:5]:  # Show first 5 errors
            error_msg = req.get('error', 'Unknown error')
            print(f"Request #{req['request_num']}: {error_msg}")
        if len(failed_requests) > 5:
            print(f"... and {len(failed_requests) - 5} more errors")
    
    print(f"\n{'='*60}")
    print(f"Completed at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'='*60}\n")

if __name__ == "__main__":
    run_load_test()