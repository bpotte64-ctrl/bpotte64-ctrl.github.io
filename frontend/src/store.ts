export let store = $store(
	{
		logs: 0,

		theme:
			window.matchMedia &&
			window.matchMedia("(prefers-color-scheme: light)").matches
				? "light"
				: "dark",

		wispServer: import.meta.env.VITE_WISP_URL || "wss://anura.pro",
		epoxyVersion: "",
		accentColor: undefined,

		analytics: true,
	},
	{ ident: "options", backing: "localstorage", autosave: "auto" }
);

if (typeof store.analytics === "undefined") store.analytics = true;

(self as any).store = store;
