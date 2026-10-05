import psycopg2

try:
    conn = psycopg2.connect(
        host='49.13.208.232',
        port=5432,
        dbname='webofwork',
        user='webofwork_user',
        password='Webofworkpassword1#'
    )
    cur = conn.cursor()

    cur.execute('''
        SELECT stage, status, count(*) 
        FROM workflow_runs 
        GROUP BY stage, status 
        ORDER BY stage, status;
    ''')
    rows = cur.fetchall()
    print('--- WORKFLOW RUNS BY STAGE AND STATUS ---')
    for row in rows:
        print(f'Stage: {row[0]:<15} Status: {row[1]:<3} Count: {row[2]}')

    print('\n--- EXAMPLES OF SUCCESSFUL RUNS ---')
    cur.execute('''
        SELECT id, stage, status, created_at, length(final_output) as output_length
        FROM workflow_runs 
        WHERE status NOT IN (0, 1, 7, 8) -- Exclude Pending, Running, Failed, Cancelled
        AND final_output IS NOT NULL
        LIMIT 5;
    ''')
    examples = cur.fetchall()
    for ex in examples:
        print(f'Run ID: {ex[0]}, Stage: {ex[1]}, Status: {ex[2]}, Created: {ex[3]}, Output Length: {ex[4]} bytes')

    conn.close()
except Exception as e:
    print(f'Error: {e}')
