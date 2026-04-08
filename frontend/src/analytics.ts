import { store } from "./store";

let debug = console.debug;

function processEvent(type: any, payload: any) {
	if (!store.analytics && payload?.name !== "analytics-off") {
		return;
	}
	debug("sending", type, payload)
	return payload;
}
(globalThis as any).processEvent = processEvent;

export let analyticsEnabled = !!(globalThis as any).umami;
let umami = (globalThis as any).umami;

export function event(name: string, args?: any) {
	try {
		if (!umami) return;
		umami.track(name, args);
	} catch(err) { debug("failed", err) }
}

useChange(store.analytics, () => {
	if (!store.analytics)
		event("analytics-off");
})
