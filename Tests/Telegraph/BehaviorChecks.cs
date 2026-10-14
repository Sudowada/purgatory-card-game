using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class MonoBehaviour { public GameObject gameObject = new GameObject(); public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>(); public T GetComponentInParent<T>(bool includeInactive=false) where T:class=>GetComponent<T>(); }
 public class GameObject { public Dictionary<Type,object> components=new Dictionary<Type,object>(); public T GetComponent<T>() where T:class=>components.TryGetValue(typeof(T),out var x)?(T)x:null; public T AddComponent<T>() where T:new(){var x=new T();components[typeof(T)]=x;return x;} }
 public class SerializeField:Attribute {} public class DisallowMultipleComponent:Attribute{} public class HeaderAttribute:Attribute{public HeaderAttribute(string s){}} public class TooltipAttribute:Attribute{public TooltipAttribute(string s){}} public class HideInInspector:Attribute{} public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b); public static int CeilToInt(float x)=>(int)Math.Ceiling(x); public static int FloorToInt(float x)=>(int)Math.Floor(x);}
 public static class Random {static System.Random rng=new System.Random(42);public static int Range(int a,int b)=>rng.Next(a,b);}
}
namespace UnityEngine.Events {public class UnityEvent<T,U>{public void Invoke(T t,U u){}}}
public enum CardPhysicalState {InHand,Offered}
public class Card3DInteractable:UnityEngine.MonoBehaviour{public Card SourceCard;public CardPhysicalState PhysicalState;}
public class CardAttackVisuals {public void SetPresentationVisible(bool b){} public bool Targeted;public void SetTargeted(bool b){Targeted=b;}public void PlayShieldBlock(){Targeted=false;} public void CancelShieldBlock(){}}
public class HorizontalCardHolder {public bool IsCardVisible(Card c)=>true;public List<Card> cards=new List<Card>();}
public enum RelicType {DoubleDecay}
public class RelicSystem {public static RelicSystem Instance;public bool SlowSkip,Protect,All,Double,Instant; public int GetRotThreshold(int n)=>n;public bool ShouldDecayThisTurn()=>!SlowSkip;public bool GetDecayAll()=>All;public bool IsDecayProtected(Card c,List<Card> h)=>Protect;public bool HasRelic(RelicType t)=>Double;public bool GetDecayRotOnStack()=>Instant;public void OnDecayPhaseEnd(List<Card> h){}}
public static class Checks {
 static int passed;static void Check(bool b,string message){if(!b)throw new Exception(message);passed++;}
 static void Set(object o,string name,object v)=>o.GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(o,v);
 static (HorizontalCardHolder,RandomEnemyTelegraph) Setup(int size,int value=10){RelicSystem.Instance=null;var h=new HorizontalCardHolder();for(int i=0;i<size;i++){var c=new Card{cardValue=value};c.InitCardStats();var p=new Card3DInteractable{SourceCard=c};c.gameObject.components[typeof(Card3DInteractable)]=p;h.cards.Add(c);}var t=new RandomEnemyTelegraph();t.Initialize(h);t.RollNextTelegraph();return(h,t);}
 public static void Main(){
 var(h,t)=Setup(8);Check(h.cards.FindAll(t.IsTargeted).Count==2,"two unique targets");var hit=h.cards.Find(t.IsTargeted);var blocked=h.cards.Find(c=>t.IsTargeted(c)&&c!=hit);var physical=blocked.GetComponentInParent<Card3DInteractable>();t.ResolveOfferedCard(physical);t.UndoStagedCard(physical);Check(t.IsTargeted(blocked),"undo restores target");t.ResolveOfferedCard(physical);t.ConfirmStagedBlocks();Check(!t.IsTargeted(blocked),"confirmed block");t.ResolveAttack(new List<Card>{blocked},false);Check(hit.decayLevel==3&&blocked.decayLevel==0,"damage only unblocked announced card");Check(h.cards.FindAll(c=>c.decayLevel>0).Count==1,"no automatic spread");Check(!t.IsTargeted(hit),"attack consumed");t.ResolveAttack(new List<Card>(),false);Check(hit.decayLevel==3,"no double resolution");
 (h,t)=Setup(1,1);t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].isDecayed,"ace rounds up to rot");
 (h,t)=Setup(2,13);t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==4,"king rounds up");t.RollNextTelegraph();t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==8,"cumulative decay");
 (h,t)=Setup(2);t.ResolveAttack(new List<Card>(),true);Check(h.cards[0].decayLevel==0&&!t.IsTargeted(h.cards[0]),"freeze consumes attack");
 (h,t)=Setup(2);RelicSystem.Instance=new RelicSystem{Protect=true};t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==0,"protection");
 (h,t)=Setup(2);RelicSystem.Instance=new RelicSystem{SlowSkip=true};t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==0,"slow rot");
 (h,t)=Setup(2);RelicSystem.Instance=new RelicSystem{Double=true};t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==6,"double decay");
 (h,t)=Setup(1,1);Set(t,"decayMultiplier",0.5f);t.ResolveAttack(new List<Card>(),false);Check(h.cards[0].decayLevel==0,"mitigation floors");
 (h,t)=Setup(1);hit=h.cards[0];t.ResolveAttack(new List<Card>{hit},false);hit.decayLevel=0;t.RollNextTelegraph();Check(t.IsTargeted(hit),"returned or reused slot retargets");
 (h,t)=Setup(0);t.ResolveAttack(new List<Card>(),false);Check(true,"empty hand safe");
 Console.WriteLine(passed+" behavior checks passed");
 }
}

