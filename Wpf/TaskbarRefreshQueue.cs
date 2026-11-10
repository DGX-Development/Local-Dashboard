using System;

// A completed shell request is not evidence that Explorer displayed the requested pixels.
internal sealed class TaskbarRefreshQueue {
    readonly Action<Action> enqueue;
    readonly Func<bool> available, refresh;
    readonly Func<string> resource;
    bool queued, running, pending, force;
    string completedResource;

    internal TaskbarRefreshQueue(Action<Action> enqueue, Func<bool> available,
        Func<string> resource, Func<bool> refresh) {
        this.enqueue=enqueue;this.available=available;this.resource=resource;this.refresh=refresh;
    }

    internal void Request(bool forceRefresh=false) {
        pending=true;force|=forceRefresh;
        Schedule();
    }

    void Schedule() {
        if(queued||running)return;
        queued=true;
        try{enqueue(Drain);}catch{queued=false;throw;}
    }

    void Drain() {
        queued=false;
        // A hidden window retains the request for its next activation.
        if(!pending||!available())return;
        bool forceNow=force;force=false;pending=false;
        string requested=resource();
        if(!forceNow&&completedResource==requested)return;
        running=true;
        try {
            if(refresh())completedResource=requested;
            else{completedResource=null;force=true;}
        } catch {
            completedResource=null;force=true;throw;
        } finally {
            running=false;
            // Preserve requests arriving during a shell call without retrying failures in a loop.
            if(pending)Schedule();
        }
    }
}
