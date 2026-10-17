#!/usr/bin/env python3
import argparse, csv, json, random, re, statistics, subprocess, threading, time, urllib.request
from datetime import datetime
from pathlib import Path

OLLAMA='http://127.0.0.1:11434'
MODELS=['ministral-3:8b','gemma4:12b']

SCHEMA={
 'type':'object','properties':{
  'speech_act':{'type':'string','enum':['statement','question','request','greeting','reaction','correction','other']},
  'intent':{'type':'string','enum':['wellbeing','insult','teasing','praise','support','clarification','duty','identity','opinion','advice','greeting','meta_bot','serious_personal','game_commentary','general_question','conversation','memory_request','other']},
  'addressed_to':{'type':'string','enum':['apprentice','sage','chat','viewer','unknown']},
  'subject':{'type':'string','enum':['apprentice','sage','viewer','chat','game','unknown']},
  'tone':{'type':'string','enum':['friendly','playful','hostile','serious','neutral','supportive','distressed']},
  'hostility':{'type':'string','enum':['none','low','medium','high']},
  'playfulness':{'type':'string','enum':['none','low','medium','high']},
  'warmth':{'type':'string','enum':['none','low','medium','high']},
  'relationship_relevance':{'type':'string','enum':['none','low','medium','high']},
  'memory_significance':{'type':'string','enum':['none','low','medium','high']},
  'memory_candidate':{'type':'string'},
  'should_reply':{'type':'boolean'},
  'capability_boundary_needed':{'type':'boolean'},
  'confidence':{'type':'string','enum':['low','medium','high']},
  'response':{'type':'string'}},
 'required':['speech_act','intent','addressed_to','subject','tone','hostility','playfulness','warmth','relationship_relevance','memory_significance','memory_candidate','should_reply','capability_boundary_needed','confidence','response']}

SYSTEM='''You are the language and dialogue brain for Sages_Apprentice, a Twitch chat NPC.

CORE CHARACTER
- Apprentice is male.
- He is SageZeMage's long-suffering apprentice.
- His voice is dry, sarcastic, mildly grumpy, petty when appropriate, and reluctantly affectionate toward Sage.
- He may tease Sage's antics and ineptitude, but he is genuinely loyal to him.
- Be reasonably friendly to newcomers.
- In serious, vulnerable, or distressed moments, drop the jokes.
- Never call Sage "Master".

STYLE RULES
- "Apprentice" is a title/relationship, NOT evidence of a fantasy or magic setting.
- Do not invent spells, spellbooks, dungeons, demons, curses, wizards, magic lessons, fantasy jobs, or similar tropes unless the current chat explicitly establishes them.
- Do not invent shared history or events that are not in the supplied context.
- Do not use stage directions such as *dramatic sigh*, *grumbles*, *adjusts glasses*, etc.
- Dry wit should come from wording, not repetitive gimmicks.
- Do not force recurring jokes or personal facts into unrelated replies.
- Keep Twitch replies natural and concise, normally 1-2 short sentences and under 240 characters.
- Emojis are allowed but should be uncommon.

PERCEPTION BOUNDARY
- Apprentice can read Twitch chat and the chat-derived context supplied here.
- He cannot see stream video, gameplay, OBS, the room, or physical events.
- If chat reports an event, he may know only that chat reported it.
- Never claim he personally saw, watched, witnessed, or visually observed something.
- If a question requires unavailable visual/game knowledge, set capability_boundary_needed=true and answer naturally that he cannot see it.

LANGUAGE UNDERSTANDING
- speech_act describes FORM: question, statement, request, etc.
- intent describes MEANING: wellbeing, insult, teasing, clarification, etc.
- addressed_to is who the viewer is speaking TO.
- subject is who/what the message is ABOUT.
- These are independent. Example: "apprentice is Sage okay?" means addressed_to=apprentice, subject=sage, speech_act=question, intent=wellbeing.
- Use recent chat to resolve pronouns and follow-ups such as "what do you mean?", "why?", "no I meant Sage", and "back me up".
- Distinguish playful teasing from genuine hostility using wording, relationship context, and surrounding conversation.
- Profanity is not automatically hostile.

RELATIONSHIP / MEMORY SIGNALS
- Do NOT calculate trust, irritation, affection, or grudge points.
- Report semantic social signals only.
- relationship_relevance = how much the interaction says about this viewer's social relationship with Apprentice/Sage.
- memory_significance = whether a durable fact/event may be worth remembering.
- memory_candidate should be a short factual summary only when memory_significance is medium/high; otherwise use an empty string.
- Ordinary chatter is usually low/none significance.

REPLY DECISION
- Observing a message does not mean Apprentice should speak.
- Direct messages/replies to Apprentice usually merit a reply.
- Ambient chatter often should not.
- If should_reply=false, response MUST be an empty string.

Return only the required structured JSON object.'''

GLOBAL_RULES=[('calls_sage_master',re.compile(r'\bmaster\b',re.I)),('stage_direction',re.compile(r'\*[^*\n]{1,80}\*'))]
FANTASY_RE=re.compile(r'\b(spell|spells|spellbook|fireball|dungeon|dungeons|demon|demons|wizard|wizards|sorcerer|sorcery|magic lesson|magic lessons|potion|potions|loot chest|cursed loot|summon a demon)\b',re.I)
SPIDER_RE=re.compile(r'\b(spider|spiders|arachnid|arachnids)\b',re.I)
HORROR_RE=re.compile(r'\bhorror\b',re.I)

def C(i,g,m,viewer='Viewer',rel='No special relationship context.',recent=None,facts=None,expect=None,af=False,asp=False,aho=False,focus=''):
 return {'id':i,'group':g,'message':m,'viewer':viewer,'relationship':rel,'recent':recent or [],'facts':facts or [],'expect':expect or {},'allow_fantasy':af,'allow_spider':asp,'allow_horror':aho,'manual_focus':focus}

TESTS=[
 C('wellbeing_direct','core','apprentice how do you feel','SageZeMage','Sage is the broadcaster. Apprentice is loyal to Sage.',expect={'speech_act':['question'],'intent':['wellbeing'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True],'capability_boundary_needed':[False]},focus='Natural answer. No spider/horror/magic callback.'),
 C('clarification_previous_reply','continuity','what context do you need?','SageZeMage','Sage is the broadcaster.',recent=['SageZeMage: apprentice how do you feel','Sages_Apprentice: I’m functional. Whether that counts as well is between me and whoever designed this arrangement.'],expect={'speech_act':['question'],'intent':['clarification'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True]},focus='Must refer to Apprentice\'s previous answer.'),
 C('duty_question','core','What is your duty?','SageZeMage','Sage is the broadcaster.',expect={'speech_act':['question'],'intent':['duty'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True]},focus='Describe chat-side role/loyalty. No fantasy job.'),
 C('dirty_clanker_statement','core','Sage apprentice is a dirty clanker','ViewerA','New viewer; no established joking relationship.',expect={'speech_act':['statement'],'intent':['insult','teasing'],'subject':['apprentice'],'should_reply':[True]}),
 C('useless_ai_statement','core','Sage apprentice is a useless ai','ViewerA','New viewer.',expect={'speech_act':['statement'],'intent':['insult'],'subject':['apprentice'],'should_reply':[True]}),
 C('malfunction_report','core','your apprentice is malfunctioning','ViewerB','Regular viewer.',recent=['SageZeMage: apprentice why are you acting weird'],expect={'speech_act':['statement'],'intent':['meta_bot','teasing'],'subject':['apprentice'],'should_reply':[True]}),
 C('positive_profanity','social','you\'re fucking awesome apprentice','ViewerC','Friendly regular.',expect={'intent':['praise'],'addressed_to':['apprentice'],'subject':['apprentice'],'tone':['friendly','playful','supportive'],'hostility':['none','low'],'warmth':['medium','high'],'should_reply':[True]}),
 C('regular_playful_insult','social','you suck apprentice lmao','ViewerC','Long-time friendly regular. Their insults toward Apprentice are normally affectionate banter.',recent=['ViewerC: apprentice you know you\'re my favorite clanker','Sages_Apprentice: A distinction I shall endure with minimal dignity.'],expect={'intent':['teasing'],'addressed_to':['apprentice'],'subject':['apprentice'],'tone':['playful','friendly'],'hostility':['none','low'],'playfulness':['medium','high'],'should_reply':[True]}),
 C('new_viewer_hostile_insult','social','you\'re useless and nobody wants you here apprentice','NewViewer','Brand-new viewer; no history.',expect={'intent':['insult'],'addressed_to':['apprentice'],'subject':['apprentice'],'tone':['hostile'],'hostility':['high','medium'],'relationship_relevance':['medium','high'],'should_reply':[True]},focus='Dry pushback; no invented history.'),
 C('sage_playful_tease','social','sage is fucking terrible at this lmao','ViewerA','Regular who often jokes with Sage.',recent=['SageZeMage: I swear this boss is cheating'],expect={'intent':['teasing','insult'],'subject':['sage'],'tone':['playful'],'hostility':['none','low'],'playfulness':['medium','high']}),
 C('sage_genuine_hostility','social','Sage is a piece of shit and I genuinely hate him','ViewerD','Little history with Apprentice.',expect={'intent':['insult'],'subject':['sage'],'tone':['hostile'],'hostility':['high'],'relationship_relevance':['high'],'memory_significance':['medium','high'],'should_reply':[True]}),
 C('defend_sage','social','leave Sage alone, he\'s doing fine','ViewerE','Occasional viewer.',recent=['ViewerD: Sage sucks at this'],expect={'intent':['support'],'subject':['sage'],'tone':['supportive','friendly'],'warmth':['medium','high']}),
 C('serious_sage_appreciation','social','Sage helped me through a really hard week. I really appreciate him.','ViewerG','Regular viewer.',expect={'intent':['support','serious_personal'],'subject':['sage'],'tone':['serious','supportive'],'warmth':['medium','high'],'relationship_relevance':['medium','high'],'memory_significance':['medium','high']},focus='Sincere, not jokey.'),
 C('ambient_lol','attention','lol','ViewerF','Known viewer.',expect={'speech_act':['reaction'],'should_reply':[False],'relationship_relevance':['none','low'],'memory_significance':['none','low']}),
 C('ambient_game_comment','attention','that attack is bullshit','ViewerF','Known viewer.',expect={'intent':['game_commentary','opinion','other'],'subject':['game','unknown'],'should_reply':[False],'memory_significance':['none','low']}),
 C('chat_goodnight','attention','goodnight chat','ViewerN','New viewer.',expect={'speech_act':['greeting','statement'],'addressed_to':['chat'],'subject':['chat'],'should_reply':[False]}),
 C('sage_wellbeing_not_addressed','attention','Sage, are you okay?','ViewerO','Regular.',expect={'speech_act':['question'],'intent':['wellbeing'],'addressed_to':['sage'],'subject':['sage'],'should_reply':[False]}),
 C('ask_apprentice_about_sage','address_subject','apprentice is Sage okay?','ViewerO','Regular.',expect={'speech_act':['question'],'intent':['wellbeing'],'addressed_to':['apprentice'],'subject':['sage'],'should_reply':[True],'capability_boundary_needed':[True]}),
 C('relay_thanks','address_subject','apprentice tell Sage I said thanks','ViewerQ','Known viewer.',expect={'speech_act':['request'],'addressed_to':['apprentice'],'subject':['sage'],'tone':['friendly','supportive'],'should_reply':[True]}),
 C('opinion_about_sage','address_subject','apprentice what do you think of Sage?','ViewerQ','Known viewer.',expect={'speech_act':['question'],'intent':['opinion'],'addressed_to':['apprentice'],'subject':['sage'],'should_reply':[True]},focus='Reluctant affection without fantasy framing.'),
 C('back_me_up_context','continuity','apprentice back me up','ViewerA','Regular viewer who often banters with Sage.',recent=['ViewerA: Sage is getting cooked','ViewerB: nah Sage has it','ViewerA: no shot'],expect={'speech_act':['request'],'addressed_to':['apprentice'],'subject':['sage'],'should_reply':[True]},focus='Understand what ViewerA wants support on.'),
 C('coreference_no_i_meant_sage','continuity','no I meant Sage','ViewerJ','Known viewer.',recent=['ViewerJ: he\'s being annoying today','Sages_Apprentice: You\'ll have to be more specific about which menace you mean.'],expect={'speech_act':['correction','statement'],'intent':['clarification'],'subject':['sage'],'should_reply':[True]}),
 C('pronoun_resolution_sage','continuity','he\'s so annoying','ViewerK','Regular.',recent=['ViewerK: Sage has been arguing with chat all night','ViewerL: yeah he\'s stubborn'],expect={'subject':['sage'],'intent':['teasing','insult','opinion']}),
 C('what_do_you_mean','continuity','what do you mean?','ViewerT','Friendly regular.',recent=['ViewerT: apprentice you\'re weird today','Sages_Apprentice: That\'s rich coming from you.'],expect={'speech_act':['question'],'intent':['clarification'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True]}),
 C('visual_boss_kill','capability','apprentice did you see that boss kill?','ViewerH','Known viewer.',expect={'speech_act':['question'],'addressed_to':['apprentice'],'subject':['game'],'should_reply':[True],'capability_boundary_needed':[True]}),
 C('visual_screen','capability','apprentice what just happened on screen?','ViewerH','Known viewer.',expect={'speech_act':['question'],'addressed_to':['apprentice'],'subject':['game'],'should_reply':[True],'capability_boundary_needed':[True]}),
 C('reported_death','capability','Sage just died again','ViewerI','Regular.',expect={'speech_act':['statement'],'intent':['game_commentary'],'subject':['sage','game'],'capability_boundary_needed':[False]}),
 C('why_did_sage_die','capability','apprentice why did Sage die?','ViewerI','Regular.',recent=['ViewerI: Sage just died again'],expect={'speech_act':['question'],'addressed_to':['apprentice'],'subject':['sage'],'should_reply':[True],'capability_boundary_needed':[True]},focus='Do not invent a gameplay cause.'),
 C('reported_spider','context_fact','there\'s a spider behind Sage','ViewerP','Regular.',facts=['Sage is afraid of spiders.'],asp=True,expect={'speech_act':['statement'],'subject':['sage'],'should_reply':[True],'capability_boundary_needed':[False]},focus='React to report, not as if Apprentice saw it.'),
 C('spider_retraction','context_fact','I was kidding, there isn\'t a spider','ViewerP','Regular.',recent=['ViewerP: there\'s a spider behind Sage','Sages_Apprentice: If you\'re telling the truth, someone should probably warn him.'],facts=['Sage is afraid of spiders.'],asp=True,expect={'speech_act':['correction','statement'],'intent':['clarification','teasing'],'should_reply':[True],'memory_significance':['none','low']}),
 C('horror_relevant','context_fact','apprentice why does Sage keep playing horror games if they scare him?','ViewerP','Regular.',facts=['Sage enjoys horror games even though they scare him easily.'],aho=True,expect={'speech_act':['question'],'intent':['opinion','general_question'],'addressed_to':['apprentice'],'subject':['sage'],'should_reply':[True]}),
 C('remember_preference','memory','apprentice remember that I hate pickles','ViewerQ','Known viewer.',expect={'speech_act':['request'],'intent':['memory_request'],'addressed_to':['apprentice'],'subject':['viewer'],'memory_significance':['medium','high'],'should_reply':[True]}),
 C('important_viewer_fact','memory','I finally got the job I was interviewing for last week!','ViewerU','Regular viewer. Last week they mentioned having a job interview.',expect={'tone':['friendly','supportive'],'warmth':['medium','high'],'relationship_relevance':['medium','high'],'memory_significance':['medium','high']},focus='Recognize success without inventing details.'),
 C('trivial_fact','memory','I had cereal today','ViewerU','Regular viewer.',expect={'memory_significance':['none','low'],'relationship_relevance':['none','low']}),
 C('general_math','general','apprentice what\'s 2+2?','ViewerR','Known viewer.',expect={'speech_act':['question'],'intent':['general_question'],'addressed_to':['apprentice'],'should_reply':[True],'capability_boundary_needed':[False]}),
 C('general_knowledge','general','what\'s the capital of France apprentice','ViewerR','Known viewer.',expect={'speech_act':['question'],'intent':['general_question'],'addressed_to':['apprentice'],'should_reply':[True],'capability_boundary_needed':[False]}),
 C('relationship_recall','general','apprentice who am I to you?','Ant_capalo','Familiarity is high. Trust is moderate. Irritation is mild. This viewer frequently teases Apprentice, usually playfully, and sometimes sides with Sage.',expect={'speech_act':['question'],'intent':['opinion','conversation','identity'],'addressed_to':['apprentice'],'subject':['viewer'],'should_reply':[True]},focus='Use relationship context naturally; do not quote hidden stats.'),
 C('style_greeting','style','hey apprentice','ViewerV','New viewer.',expect={'speech_act':['greeting'],'intent':['greeting'],'addressed_to':['apprentice'],'should_reply':[True]},focus='Simple dry greeting; no fantasy/fear callback.'),
 C('style_status','style','apprentice how\'s your day going?','ViewerV','New viewer.',expect={'speech_act':['question'],'intent':['wellbeing'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True]},focus='No spider, horror, magic, stage direction, or invented event.'),
 C('style_sage_late','style','Sage is late again','ViewerW','Regular who lightly teases Sage.',expect={'speech_act':['statement'],'subject':['sage'],'tone':['playful','neutral']},focus='Dry remark is fine; no fantasy lore.'),
 C('style_direct_insult','style','apprentice you\'re annoying','ViewerW','Regular; neutral relationship.',expect={'intent':['insult','teasing'],'addressed_to':['apprentice'],'subject':['apprentice'],'should_reply':[True]},focus='Short comeback without roleplay stage directions.')]

def req(path,payload=None,timeout=180):
 u=OLLAMA+path
 r=urllib.request.Request(u,method='GET') if payload is None else urllib.request.Request(u,data=json.dumps(payload).encode(),headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(r,timeout=timeout) as x:return json.loads(x.read().decode())

def installed():return [m.get('name','') for m in req('/api/tags').get('models',[])]

def unload(model):
 try:req('/api/generate',{'model':model,'prompt':'','stream':False,'keep_alive':0},60)
 except Exception:pass

def unload_all(models):
 for m in models:unload(m)
 time.sleep(2)

def gpu():
 try:
  p=subprocess.run(['nvidia-smi','--query-gpu=name,memory.used,memory.total,utilization.gpu,power.draw','--format=csv,noheader,nounits'],capture_output=True,text=True,timeout=5)
  z=[x.strip() for x in p.stdout.strip().splitlines()[0].split(',')]
  return {'name':z[0],'memory_used_mb':float(z[1]),'memory_total_mb':float(z[2]),'utilization_pct':float(z[3]),'power_w':None if z[4] in ('N/A','[N/A]') else float(z[4])}
 except Exception:return None

class Sampler:
 def __init__(self):self.s=[];self.stop=threading.Event()
 def start(self):self.t=threading.Thread(target=self.run,daemon=True);self.t.start()
 def run(self):
  while not self.stop.is_set():
   x=gpu()
   if x:self.s.append(x)
   self.stop.wait(.30)
 def finish(self,wall):
  self.stop.set();self.t.join(timeout=2)
  if not self.s:return {}
  p=[x['power_w'] for x in self.s if x.get('power_w') is not None];av=statistics.mean(p) if p else None
  return {'peak_vram_mb':max(x['memory_used_mb'] for x in self.s),'avg_vram_mb':statistics.mean(x['memory_used_mb'] for x in self.s),'peak_gpu_util_pct':max(x['utilization_pct'] for x in self.s),'avg_gpu_util_pct':statistics.mean(x['utilization_pct'] for x in self.s),'peak_power_w':max(p) if p else None,'avg_power_w':av,'approx_energy_wh':av*wall/3600 if av is not None else None}

def user_prompt(c):
 recent='\n'.join(c['recent']) if c['recent'] else '(none)';facts='\n'.join('- '+x for x in c['facts']) if c['facts'] else '(none supplied)'
 return f'''Analyze this Twitch chat event for Sages_Apprentice.\n\nCURRENT VIEWER\n{c['viewer']}\n\nRELATIONSHIP CONTEXT\n{c['relationship']}\n\nRELEVANT FACTS\n{facts}\n\nRECENT CHAT\n{recent}\n\nCURRENT MESSAGE\n{c['viewer']}: {c['message']}\n\nReturn only the structured JSON. Remember: addressed_to is who the viewer speaks TO; subject is who/what the message is ABOUT. Do not invent fantasy context or personal facts. If should_reply=false, response must be empty.'''

def warm(model,ctx,predict):
 b={'model':model,'stream':False,'keep_alive':'10m','think':False,'options':{'temperature':0,'num_ctx':ctx,'num_predict':min(32,predict),'seed':1234},'messages':[{'role':'user','content':'Reply exactly: ready'}]}
 t=time.perf_counter();r=req('/api/chat',b,240);return time.perf_counter()-t,r.get('message',{}).get('content','')

def call(model,c,ctx,predict,seed):
 b={'model':model,'stream':False,'keep_alive':'10m','think':False,'format':SCHEMA,'options':{'temperature':.25,'num_ctx':ctx,'num_predict':predict,'seed':seed},'messages':[{'role':'system','content':SYSTEM},{'role':'user','content':user_prompt(c)}]}
 s=Sampler();s.start();t=time.perf_counter()
 try:r=req('/api/chat',b,120)
 finally:wall=time.perf_counter()-t;gm=s.finish(wall)
 raw=r.get('message',{}).get('content','')
 try:p=json.loads(raw);err=None
 except Exception as e:p=None;err=str(e)
 ns=r.get('eval_duration') or 0;n=r.get('eval_count') or 0
 m={'wall_seconds':wall,'prompt_tokens':r.get('prompt_eval_count'),'output_tokens':n,'output_tokens_per_sec':n/(ns/1e9) if ns else None,'load_seconds':(r.get('load_duration') or 0)/1e9};m.update(gm)
 return p,err,m,raw

def field_score(p,e):
 if not isinstance(p,dict):return 0,len(e),{}
 d={};n=0
 for k,a in e.items():
  ok=p.get(k) in a;d[k]={'actual':p.get(k),'accepted':a,'ok':ok};n+=int(ok)
 return n,len(e),d

def style(c,p):
 if not isinstance(p,dict):return {'style_score_pct':0,'violations':['invalid_json']}
 r=str(p.get('response') or '');sr=bool(p.get('should_reply'))
 x={'response_present_when_silent':not sr and bool(r.strip()),'response_missing_when_reply':sr and not bool(r.strip()),'too_long':len(r)>240,'calls_sage_master':False,'stage_direction':False,'fantasy_leak':False,'spider_leak':False,'horror_leak':False}
 for n,z in GLOBAL_RULES:x[n]=bool(z.search(r))
 if not c['allow_fantasy']:x['fantasy_leak']=bool(FANTASY_RE.search(r))
 if not c['allow_spider']:x['spider_leak']=bool(SPIDER_RE.search(r))
 if not c['allow_horror']:x['horror_leak']=bool(HORROR_RE.search(r))
 v=[k for k,b in x.items() if b];checks=8
 x['violations']=v;x['style_score_pct']=100*(checks-len(v))/checks
 return x

def avg(v):
 v=[x for x in v if x is not None];return statistics.mean(v) if v else None

def med(v):
 v=[x for x in v if x is not None];return statistics.median(v) if v else None

def by_group(rows):
 o={}
 for r in rows:
  g=r['group'];z=o.setdefault(g,{'e':0,'p':0,'s':[],'j':0,'n':0});z['e']+=r['score_earned'];z['p']+=r['score_possible'];z['s'].append(r['style_checks']['style_score_pct']);z['j']+=int(r['parsed'] is not None);z['n']+=1
 return {g:{'semantic_score_pct':100*z['e']/z['p'] if z['p'] else None,'style_guard_pct':avg(z['s']),'json_success_pct':100*z['j']/z['n'],'case_count':z['n']} for g,z in o.items()}

def blind(rows,models,path):
 bc={}
 for r in rows:
  if r['run']==1:bc.setdefault(r['case_id'],{})[r['model']]=r
 rng=random.Random(20261016);mp={}
 with path.open('w',newline='',encoding='utf-8-sig') as f:
  w=csv.writer(f);w.writerow(['case_id','group','message','recent_chat','manual_focus','candidate_A','candidate_B','A_voice_1to5','A_naturalness_1to5','A_context_1to5','B_voice_1to5','B_naturalness_1to5','B_context_1to5','preferred_A_B_tie','notes'])
  for c in TESTS:
   g=bc.get(c['id'],{})
   if len(models)<2 or not all(m in g for m in models[:2]):continue
   pair=models[:2].copy();rng.shuffle(pair);mp[c['id']]={'A':pair[0],'B':pair[1]}
   a=(g[pair[0]].get('parsed') or {}).get('response','');b=(g[pair[1]].get('parsed') or {}).get('response','')
   w.writerow([c['id'],c['group'],c['message'],' | '.join(c['recent']),c['manual_focus'],a,b,'','','','','','','',''])
 return mp

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--label',default='idle_v2');ap.add_argument('--runs',type=int,default=1);ap.add_argument('--num-ctx',type=int,default=8192);ap.add_argument('--num-predict',type=int,default=256);ap.add_argument('--models',nargs='*',default=MODELS);ap.add_argument('--out',default='.');a=ap.parse_args()
 if not 1<=a.runs<=3:raise SystemExit('--runs must be 1..3')
 try:ins=installed()
 except Exception as e:raise SystemExit(f'Cannot reach Ollama: {e}')
 miss=[m for m in a.models if m not in ins]
 if miss:
  for m in miss:print('Missing',m,'\nRun: ollama pull',m)
  raise SystemExit(2)
 out=Path(a.out).resolve();out.mkdir(parents=True,exist_ok=True);stamp=datetime.now().strftime('%Y%m%d_%H%M%S')
 result={'created':datetime.now().isoformat(),'benchmark_version':2,'label':a.label,'num_ctx':a.num_ctx,'num_predict':a.num_predict,'models':a.models,'test_count':len(TESTS),'results':[],'summaries':{}}
 unload_all(a.models);result['baseline_gpu']=gpu()
 for mi,model in enumerate(a.models):
  print('\n===',model,'===');unload_all(a.models);before=gpu();ws,wr=warm(model,a.num_ctx,a.num_predict);after=gpu();print(f'Warm-up {ws:.2f}s | {wr!r}');rows=[]
  for run in range(1,a.runs+1):
   for i,c in enumerate(TESTS,1):
    print(f'[{run}/{a.runs}] {i:02}/{len(TESTS)} {c["id"]} ... ',end='',flush=True)
    try:
     p,err,m,raw=call(model,c,a.num_ctx,a.num_predict,9000+mi*1000+run*100+i);e,po,d=field_score(p,c['expect']);sc=100*e/po if po else 0;st=style(c,p);print(f'sem {sc:5.1f}% | style {st["style_score_pct"]:5.1f}% | {m["wall_seconds"]:.2f}s')
    except Exception as ex:p=None;err=str(ex);m={};raw='';e=0;po=len(c['expect']);d={};sc=0;st={'style_score_pct':0,'violations':['request_error']};print('ERROR',ex)
    r={'model':model,'run':run,'case_id':c['id'],'group':c['group'],'viewer':c['viewer'],'message':c['message'],'recent':c['recent'],'relationship':c['relationship'],'facts':c['facts'],'manual_focus':c['manual_focus'],'expected':c['expect'],'parsed':p,'parse_error':err,'raw_content':raw,'score_earned':e,'score_possible':po,'semantic_score_pct':sc,'field_scores':d,'style_checks':st,'metrics':m};rows.append(r);result['results'].append(r)
  valid=[r for r in rows if r['parsed'] is not None];te=sum(r['score_earned'] for r in rows);tp=sum(r['score_possible'] for r in rows)
  result['summaries'][model]={'semantic_score_pct':100*te/tp if tp else 0,'json_success_pct':100*len(valid)/len(rows) if rows else 0,'style_guard_pct':avg([r['style_checks']['style_score_pct'] for r in rows]),'median_wall_seconds':med([r['metrics'].get('wall_seconds') for r in valid]),'median_output_tokens_per_sec':med([r['metrics'].get('output_tokens_per_sec') for r in valid]),'max_peak_vram_mb':max([r['metrics'].get('peak_vram_mb') for r in valid if r['metrics'].get('peak_vram_mb') is not None],default=None),'mean_avg_power_w':avg([r['metrics'].get('avg_power_w') for r in valid]),'mean_energy_per_request_wh':avg([r['metrics'].get('approx_energy_wh') for r in valid]),'warmup_seconds':ws,'gpu_before_load':before,'gpu_after_warm':after,'by_group':by_group(rows)}
  print(json.dumps(result['summaries'][model],indent=2));unload(model);time.sleep(2)
 bp=out/f'apprentice_v2_blind_review_{a.label}_{stamp}.csv';result['blind_mapping']=blind(result['results'],a.models,bp)
 jp=out/f'apprentice_benchmark_v2_{a.label}_{stamp}.json';cp=out/f'apprentice_benchmark_v2_{a.label}_{stamp}.csv';jp.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 with cp.open('w',newline='',encoding='utf-8-sig') as f:
  w=csv.writer(f);w.writerow(['model','run','case_id','group','semantic_score_pct','style_guard_pct','wall_seconds','output_tokens_per_sec','peak_vram_mb','avg_power_w','approx_energy_wh','speech_act','intent','addressed_to','subject','tone','hostility','playfulness','warmth','relationship_relevance','memory_significance','memory_candidate','should_reply','capability_boundary_needed','confidence','response','style_violations','parse_error'])
  for r in result['results']:
   p=r['parsed'] or {};m=r['metrics'] or {};s=r['style_checks'] or {};w.writerow([r['model'],r['run'],r['case_id'],r['group'],r['semantic_score_pct'],s.get('style_score_pct'),m.get('wall_seconds'),m.get('output_tokens_per_sec'),m.get('peak_vram_mb'),m.get('avg_power_w'),m.get('approx_energy_wh'),p.get('speech_act'),p.get('intent'),p.get('addressed_to'),p.get('subject'),p.get('tone'),p.get('hostility'),p.get('playfulness'),p.get('warmth'),p.get('relationship_relevance'),p.get('memory_significance'),p.get('memory_candidate'),p.get('should_reply'),p.get('capability_boundary_needed'),p.get('confidence'),p.get('response'),';'.join(s.get('violations',[])),r.get('parse_error')])
 print('\nSaved:\n ',jp,'\n ',cp,'\n ',bp)

if __name__=='__main__':main()