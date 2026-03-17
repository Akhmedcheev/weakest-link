import requests, json, time, re

key = 'AIzaSyA_6_70pufshcRrA9xAK79q6YL-fQXiy7Q'
url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={key}'

# Load what we already got
existing_new = json.load(open(r'i:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\new_questions_500.json', 'r', encoding='utf-8'))
max_id = max(q['Id'] for q in existing_new) if existing_new else 301
results = list(existing_new)
print(f"Already have {len(results)} questions, max ID={max_id}")

needed = 500 - len(results)
batches = (needed // 50) + 1

for batch in range(batches):
    sid = max_id + 1 + batch * 50
    prompt = (
        f"Сгенерируй ровно 50 вопросов для телевикторины Слабое Звено. "
        f"8 из них должны быть вопросами с выбором через ИЛИ (пример: Москва или Петербург - какой город стоит на Неве?). "
        f"Остальные - на общие знания. Короткие вопросы, ответы 1-3 слова. "
        f"JSON массив, формат: "
        f'[{{"Id":{sid},"Text":"вопрос?","Answer":"ответ","AcceptableAnswers":"ответ"}}]. '
        f"Начни с Id {sid}. ТОЛЬКО JSON. Никакого markdown."
    )
    body = {
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {"temperature": 0.9, "maxOutputTokens": 8000}
    }
    for attempt in range(3):
        try:
            r = requests.post(url, json=body, timeout=90)
            if r.status_code != 200:
                print(f"  Batch {batch} attempt {attempt}: HTTP {r.status_code}")
                time.sleep(5)
                continue
            txt = r.json()["candidates"][0]["content"]["parts"][0]["text"].strip()
            if txt.startswith("```"):
                txt = txt.split("\n", 1)[1]
            if txt.endswith("```"):
                txt = txt.rsplit("\n", 1)[0]
            qs = json.loads(txt)
            results.extend(qs)
            max_id = max(q['Id'] for q in results)
            print(f"Batch {batch}: +{len(qs)} (total {len(results)})")
            time.sleep(2)
            break
        except Exception as ex:
            print(f"  Batch {batch} attempt {attempt}: {str(ex)[:80]}")
            time.sleep(3)
    if len(results) >= 500:
        break

# Renumber IDs sequentially from 302
for i, q in enumerate(results):
    q['Id'] = 302 + i

out_path = r'i:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\new_questions_500.json'
with open(out_path, 'w', encoding='utf-8') as f:
    json.dump(results[:500], f, ensure_ascii=False, indent=2)
print(f"\nFinal: {min(len(results),500)} questions saved to {out_path}")
