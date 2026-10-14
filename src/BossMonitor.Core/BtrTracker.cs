namespace BossMonitor.Core;

public record BtrLocationEstimate(string Summary,string Detail,DateTime? UpdatedAt,string Key);

public static class BtrTracker
{
    private const int ReadyToMoveSeconds=8;
    private static readonly HashSet<string> CheckpointStarts=new(StringComparer.OrdinalIgnoreCase)
    {
        "ZoneHouse","ZoneMiniHouse","ZoneRoad"
    };

    public static BtrLocationEstimate? Estimate(RaidState raid,DateTime now)
    {
        if(raid.Btr is not BossState btr || btr.Status!="Confirmed")return null;
        if(raid.Ended)return new(Location(I18n.T("레이드 종료","战局已结束")),"",raid.EndedAt,"ended");
        if(!raid.Map.Equals("Woods",StringComparison.OrdinalIgnoreCase))
        {
            string summary=raid.BtrReadyAt.Count==0
                ? I18n.T("현재 위치 확인 대기","等待确认当前位置")
                : string.Format(I18n.T("이동 신호 {0}회 감지 · 현재 위치 확인 불가","检测到 {0} 次移动信号 · 无法确认当前位置"),raid.BtrReadyAt.Count);
            return new(Location(summary),I18n.T("현재 위치는 로그 기반 추정 기능이 지원되는 맵에서만 표시됩니다.","当前位置仅在支持日志推测的地图上显示。"),raid.BtrReadyAt.LastOrDefault(),"unsupported|"+raid.BtrReadyAt.Count);
        }

        var signals=raid.BtrReadyAt.Where(t=>btr.At==null || t>=btr.At.Value).OrderBy(t=>t).ToList();
        if(signals.Count==0)
        {
            double seconds=btr.At is DateTime activated ? Math.Max(0,(now-activated).TotalSeconds) : 0;
            bool checkpoint=CheckpointStarts.Contains(btr.Zone);
            string stop=checkpoint?Stop("checkpoint"):I18n.T("첫 정류장","首个停车点");
            string state=seconds<260
                ? string.Format(I18n.T("{0} 방면 이동 중 (추정)","正在前往{0}（推测）"),stop)
                : string.Format(I18n.T("{0} 정차 중 (추정)","停靠在{0}（推测）"),stop);
            return new(Location(state),I18n.T("아직 출발 준비 신호가 없습니다.","尚未检测到发车准备信号。"),btr.At,$"initial|{checkpoint}|{seconds>=260}");
        }

        // Buying a taxi can send the BTR away from its patrol route. EFT logs the
        // purchase and departure, but not the selected destination, so continuing
        // the stop-count route here would confidently show the wrong place.
        DateTime latestSignal=signals[^1];
        DateTime? taxi=raid.BtrTaxiAt
            .Where(t=>t>=latestSignal.AddSeconds(-15) && t<=latestSignal.AddSeconds(15))
            .OrderByDescending(t=>t).Cast<DateTime?>().FirstOrDefault();
        if(taxi is DateTime taxiAt)
        {
            string summary=I18n.T("승객 지정 경로 이동 중 (목적지 확인 불가)","按乘客指定路线行驶（无法确认目的地）");
            string detail=string.Format(I18n.T("택시 이용 {0:HH:mm:ss} · 자동 순환 경로 추정 일시 중지","乘车服务 {0:HH:mm:ss} · 已暂停自动巡逻路线推测"),taxiAt);
            return new(Location(summary),detail,latestSignal,$"taxi|{latestSignal:O}");
        }

        bool route2=signals.Count>=3 && (signals[2]-signals[1]).TotalSeconds<210;
        if(route2)return Route2(signals,now);
        return Northern(signals,now);
    }

    private static BtrLocationEstimate Route2(List<DateTime> signals,DateTime now)
    {
        string[] stops=["checkpoint","sawmill","junction","oldSawmill","trainDepot","emercomBase"];
        int[] travel=[40,30,90,55,110,90];
        int index=(signals.Count-1)%stops.Length;
        return Leg(stops[index],stops[(index+1)%stops.Length],travel[index],signals[^1],now,"r2");
    }

    private static BtrLocationEstimate Northern(List<DateTime> signals,DateTime now)
    {
        if(signals.Count==1)return Leg("checkpoint","sawmill",40,signals[^1],now,"north1");
        if(signals.Count==2)
        {
            string summary=string.Format(I18n.T("{0} 출발 · {1}/{2} 방면 (추정)","已从{0}出发 · 前往{1}/{2}（推测）"),Stop("sawmill"),Stop("junction"),Stop("scavBunker"));
            return new(Location(summary),SignalDetail(signals[^1]),signals[^1],"branch|"+(int)(now-signals[^1]).TotalSeconds/15);
        }
        if(signals.Count==3)return Leg("scavBunker","sunkenVillage",65,signals[^1],now,"north3");
        if(signals.Count==4)
        {
            string summary=string.Format(I18n.T("{0} 출발 · {1}/{2} 방면 (추정)","已从{0}出发 · 前往{1}/{2}（推测）"),Stop("sunkenVillage"),Stop("oldSawmill"),Stop("trainDepot"));
            return new(Location(summary),SignalDetail(signals[^1]),signals[^1],"north4|"+(int)(now-signals[^1]).TotalSeconds/15);
        }
        string unknown=string.Format(I18n.T("북부 순환 노선 이동 중 · 이동 신호 {0}회 (추정)","北部环线行驶中 · 移动信号 {0} 次（推测）"),signals.Count);
        return new(Location(unknown),SignalDetail(signals[^1]),signals[^1],"north-many|"+signals.Count);
    }

    private static BtrLocationEstimate Leg(string from,string to,int travelSeconds,DateTime signal,DateTime now,string key)
    {
        double elapsed=Math.Max(0,(now-signal).TotalSeconds);
        string summary;
        string phase;
        if(elapsed<ReadyToMoveSeconds)
        {
            summary=string.Format(I18n.T("{0} 출발 준비","{0}准备发车"),Stop(from));phase="ready";
        }
        else if(elapsed<ReadyToMoveSeconds+travelSeconds)
        {
            summary=string.Format(I18n.T("{0} → {1} 이동 중 (추정)","{0} → {1} 行驶中（推测）"),Stop(from),Stop(to));phase="moving";
        }
        else
        {
            summary=string.Format(I18n.T("{0} 정차 중 (추정)","停靠在{0}（推测）"),Stop(to));phase="stopped";
        }
        return new(Location(summary),SignalDetail(signal),signal,$"{key}|{phase}");
    }

    private static string Location(string value)=>I18n.T("현재 위치: ","当前位置：")+value;
    private static string SignalDetail(DateTime at)=>string.Format(I18n.T("마지막 출발 준비 신호 {0:HH:mm:ss}","最后发车准备信号 {0:HH:mm:ss}"),at);
    private static string Stop(string id)=>id switch
    {
        "checkpoint"=>I18n.T("검문소","检查站"),
        "sawmill"=>I18n.T("제재소","锯木厂"),
        "junction"=>I18n.T("교차로","路口"),
        "scavBunker"=>I18n.T("스캐브 벙커","Scav 地堡"),
        "sunkenVillage"=>I18n.T("침수 마을","沉没村庄"),
        "oldSawmill"=>I18n.T("구 제재소","旧锯木厂"),
        "trainDepot"=>I18n.T("기차 차량기지","火车车库"),
        "emercomBase"=>I18n.T("에머컴 기지","EMERCOM 基地"),
        _=>id
    };
}
