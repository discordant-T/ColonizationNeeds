"""Local D1 adapter for testing the actual Worker SQL against SQLite."""
import json, sqlite3, sys
payload=json.load(sys.stdin)
db=sqlite3.connect(sys.argv[1]); db.row_factory=sqlite3.Row
db.setlimit(sqlite3.SQLITE_LIMIT_COMPOUND_SELECT,5)  # Match D1's compound SELECT limit.
try:
    if 'schema' in payload:
        db.executescript(payload['schema']); result=[]
    else:
        db.execute('PRAGMA foreign_keys=ON')
        result=[]
        with db:
            for query in payload['queries']:
                cursor=db.execute(query['sql'],query.get('args',[]))
                rows=[dict(row) for row in cursor.fetchall()]
                result.append({'results':rows,'success':True})
    print(json.dumps({'result':result}))
except Exception as exc:
    print(json.dumps({'error':str(exc)})); sys.exit(1)
finally: db.close()
