import { RingBuffer } from "ring-buffer-ts";
import { DotnetHostBuilder } from "./dotnetdefs";
import { rootFolder } from "../fs";
import { SteamJS } from "../achievements";
import { JsSplash } from "./loading";
import { STEAM_ENABLED } from "../main";
import { epoxyFetch, EpxTcpWs, EpxWs, getWispUrl } from "../epoxy";
import { steamState } from "../steam";

export type Log = { color: string, log: string };
export const TIMEBUF_SIZE = 60;
export const gameState: Stateful<{
	ready: boolean,
	initting: boolean,
	playing: boolean,

	timebuf: RingBuffer<number>,
}> = $state({
	ready: false,
	initting: false,
	playing: false,

	timebuf: new RingBuffer<number>(TIMEBUF_SIZE)
});
export const loglisteners: ((log: Log) => void)[] = [];

function proxyConsole(name: string, color: string) {
	// @ts-expect-error ts sucks
	const old = console[name].bind(console);
	// @ts-expect-error ts sucks
	console[name] = (...args) => {
		let str;
		try {
			str = args.join(" ");
		} catch {
			str = "<failed to render>";
		}
		old(...args);
		for (const logger of loglisteners) {
			logger({ color, log: str });
		}
	}
}
proxyConsole("error", "var(--error)");
proxyConsole("warn", "var(--warning)");
proxyConsole("log", "var(--fg)");
proxyConsole("info", "var(--info)");
proxyConsole("debug", "var(--fg4)");

function hookfmod() {
	let contexts: AudioContext[] = [];

	let ctx = AudioContext;
	(AudioContext as any) = function() {
		let context = new ctx();

		contexts.push(context);
		return context;
	};

	window.addEventListener("focus", async () => {
		for (let context of contexts) {
			try {
				await context.resume();
			} catch { }
		}
	});
	window.addEventListener("blur", async () => {
		for (let context of contexts) {
			try {
				await context.suspend();
			} catch { }
		}
	});
}
hookfmod();

useChange([gameState.playing], () => {
	try {
		if (gameState.playing) {
			// @ts-expect-error
			navigator.keyboard.lock()
		} else {
			// @ts-expect-error
			navigator.keyboard.unlock();
		}
	} catch (err) { console.log("keyboard lock error:", err); }
});

const wasm = await eval(`import("/_framework/dotnet.js")`);
const dotnet: DotnetHostBuilder = wasm.dotnet;
let exports: any;

export async function getDlls(): Promise<(readonly [string, string])[]> {
	const resources: any = await fetch("/_framework/blazor.boot.json").then(r => r.json());
	const whitelist = [
		"netstandard.dll",
		"mscorlib.dll",
		"System.Collections.Concurrent.dll",
		"System.Memory.dll",
		"System.Private.CoreLib.dll",
		"System.Private.Uri.dll",
		"System.Runtime.dll",
		"System.Reflection.dll",
		"System.Runtime.InteropServices.dll",
		"System.Text.RegularExpressions.dll",

		"NETCoreifier.dll",
		"FNA.dll",
		"Wasm.Celeste.dll",
		"Celeste.Wasm.mm.dll",

		"MonoMod.Common.dll",
		"MonoMod.Core.dll",
		"MonoMod.Patcher.dll",
		"MonoMod.ILHelpers.dll",
		"MonoMod.Backports.dll",
		"MonoMod.Utils.dll",
		"MonoMod.RuntimeDetour.dll",
		"Mono.Cecil.dll",
		"System.Diagnostics.Process.dll",
		"System.ComponentModel.Primitives.dll",
		"System.Collections.dll",
		"System.dll",
		"Steamworks.NET.dll",
		"Jdenticon.dll",
		"YamlDotNet.dll",
		"MAB.DotIgnore.dll",
		"Newtonsoft.Json.dll",
		"NLua.dll",
		"KeraLua.dll",
		"DnsOverHttps.dll",
	];

	return Object.entries(resources.resources.fingerprinting).map(x => [x[0] as string, x[1] as string] as const).filter(([_, v]) => whitelist.includes(v));
}

// the funny custom rsa
// https://github.com/MercuryWorkshop/wispcraft/blob/main/src/connection/crypto.ts
function encryptRSA(data: Uint8Array, n: bigint, e: bigint): Uint8Array {
	const modExp = (base: bigint, exp: bigint, mod: bigint) => {
		let result = 1n;
		base = base % mod;
		while (exp > 0n) {
			if (exp % 2n === 1n) {
				result = (result * base) % mod;
			}
			exp = exp >> 1n;
			base = (base * base) % mod;
		}
		return result;
	};
	// thank you jippity
	const pkcs1v15Pad = (messageBytes: Uint8Array, n: bigint) => {
		const messageLength = messageBytes.length;
		const nBytes = Math.ceil(n.toString(16).length / 2);

		if (messageLength > nBytes - 11) {
			throw new Error("Message too long for RSA encryption");
		}

		const paddingLength = nBytes - messageLength - 3;
		const padding = Array(paddingLength).fill(0xff);

		return BigInt(
			"0x" +
			[
				"00",
				"02",
				...padding.map((byte) => byte.toString(16).padStart(2, "0")),
				"00",
				...Array.from(messageBytes).map((byte: any) =>
					byte.toString(16).padStart(2, "0")
				),
			].join("")
		);
	};
	const paddedMessage = pkcs1v15Pad(data, n);
	let int = modExp(paddedMessage, e, n);

	let hex = int.toString(16);
	if (hex.length % 2) {
		hex = "0" + hex;
	}

	// ????
	return new Uint8Array(
		Array.from(hex.match(/.{2}/g) || []).map((byte) => parseInt(byte, 16))
	);
}

export async function downloadEverest() {
	const branch = "stable"
	const res = await fetch("https://everestapi.github.io/everestupdater.txt");
	const versionsUrl = await res.text();
	const versRes = await fetch(versionsUrl + "?supportsNativeBuilds=true");

	const versions = await versRes.json();
	const build = versions.filter((v: any) => v.branch == branch)[0];

	console.log(`Installing Everest ${branch} ${build.commit} ${build.date}`);
	console.log("Downloading Everest from", build.mainDownload);
	const zipres = await fetch(build.mainDownload);
	const zipbin = await zipres.arrayBuffer();

	const file = await rootFolder.getFileHandle("everest.zip", { create: true });
	const writable = await file.createWritable();
	await writable.write(new Uint8Array(zipbin));
	await writable.close();

	console.log("Successfully downloaded Everest");
}

export async function wispSanityCheck() {
	return;
	let r;
	try {
		r = await epoxyFetch("https://google.com");
	} catch (e) {
		console.error(e);
	}

	if (!r || !r.ok) throw new Error("wisp sanity check failed");
}

let downloadsFolder: FileSystemDirectoryHandle | null = null;

export async function pickDownloadsFolder() {
	let d = await showDirectoryPicker();
	downloadsFolder = d;
}

let libcurlresolver: any;
export const loadedLibcurlPromise = new Promise(r => libcurlresolver = r);
export async function preInit() {
	console.debug("initializing dotnet");
	const runtime = await dotnet.withConfig({
		pthreadPoolInitialSize: 24,
		// pthreadPoolUnusedSize: 512,
	}).withRuntimeOptions([
		// jit functions quickly and jit more functions
		`--jiterpreter-minimum-trace-hit-count=${500}`,
		// monitor jitted functions for less time
		`--jiterpreter-trace-monitoring-period=${100}`,
		// reject less funcs
		`--jiterpreter-trace-monitoring-max-average-penalty=${150}`,
		// increase jit function limits
		`--jiterpreter-wasm-bytes-limit=${64 * 1024 * 1024}`,
		`--jiterpreter-table-size=${16 * 1024}`,
		// print jit stats
		`--jiterpreter-stats-enabled`
	]).create();

	runtime.setModuleImports("SteamJS", SteamJS);
	runtime.setModuleImports("JsSplash", JsSplash);

	console.log("loading epoxy");
	await wispSanityCheck();

	window.WebSocket = new Proxy(WebSocket, {
		construct(t, a, n) {
			const url = new URL(a[0]);
			if (a[0] === getWispUrl() || url.host === location.host)
				return Reflect.construct(t, a, n);
			if (url.hostname.startsWith("__celestewasm_wisp_proxy_ws__"))
				return new EpxTcpWs(url.pathname.substring(1), url.hostname.replace("__celestewasm_wisp_proxy_ws__", ""));

			// @ts-expect-error
			return new EpxWs(...a);
		}
	});

	let nativefetch = window.fetch;
	let dl = document.createElement("a");
	dl.style.display = "none";
	document.body.appendChild(dl);


	window.fetch = async (...args) => {
		// don't try native for steam depots
		if (typeof args[0] !== "string" || !args[0].includes("/depot/")) {
			try {
				return await nativefetch(...args);
			} catch (e) {
			}
		} else if (downloadsFolder != null) {
			let last = args[0].split("/").pop()!;
			try {
				let file = await downloadsFolder.getFileHandle(last, { create: false });
				let h = await file.getFile();
				console.log("got file cached", last);
				return new Response(h.stream());
			} catch { }
			dl.download = "cross origin lol";
			dl.href = args[0];
			dl.click();

			while (true) {
				try {
					let file = await downloadsFolder.getFileHandle(last, { create: false });
					let h = await file.getFile();
					console.log("got file", last);
					return new Response(h.stream());
				} catch { }
				await new Promise(r => setTimeout(r, 100));
			}
		}

		// @ts-expect-error
		return await epoxyFetch(...args);
	}
	libcurlresolver();

	const config = runtime.getConfig();
	exports = await runtime.getAssemblyExports(config.mainAssemblyName!);

	runtime.setModuleImports("Celeste.js", {
		requestframe: (frametime: number) => {
			gameState.timebuf.add(frametime);
			//return new Promise(requestAnimationFrame);
			return new Promise(r=>queueMicrotask(r as any));
		}
	});

	// TODO: replace with native openssl
	runtime.setModuleImports("interop.js", {
		encryptrsa: (publicKeyModulusHex: string, publicKeyExponentHex: string, data: Uint8Array) => {
			let modulus = BigInt("0x" + publicKeyModulusHex);
			let exponent = BigInt("0x" + publicKeyExponentHex);
			let encrypted = encryptRSA(data, modulus, exponent);
			return new Uint8Array(encrypted);
		}
	});

	runtime.setModuleImports("depot.js", {
		newqr: (qr: string) => {
			console.log("QR DATA" + qr);
			steamState.qr = qr;
		}
	});

	(self as any).wasm = {
		Module: runtime.Module,
		// @ts-expect-error
		FS: runtime.Module.FS,
		dotnet,
		runtime,
		config,
		exports,
	};

	const dlls = await getDlls();

	console.debug("runMain...");
	await runtime.runMain();
	console.debug("MountFilesystems...");
	await exports.CelesteBootstrap.MountFilesystems(dlls.map(x => `${x[0]}|${x[1]}`));
	console.debug("PreInit...");
	await exports.CelesteLoader.PreInit();
	console.debug("dotnet initialized");


	if (STEAM_ENABLED) {
		await exports.Steam.Init();
		if (await exports.Steam.InitSteamSaved()) {
			console.log("Steam saved login success");
			steamState.login = 2;
		}
	}

	gameState.ready = true;
};

export async function PatchCeleste(installEverest: boolean) {
	try {
		await (await (await rootFolder.getDirectoryHandle("Celeste")).getDirectoryHandle("Everest")).getFileHandle("Celeste.Mod.mm.dll", { create: false });
	} catch {
		try {
			await rootFolder.getFileHandle("everest.zip", { create: false });
		} catch {
			await downloadEverest();
		}
		await exports.Patcher.ExtractEverest();
	}

	await exports.Patcher.PatchCeleste(installEverest);
}

export async function initSteam(username: string | null, password: string | null, qr: boolean) {
	return await exports.Steam.InitSteam(username, password, qr);
}

export async function downloadApp() {
	return await exports.Steam.DownloadApp();
}
const SEAMLESSCOUNT = 10;

export async function play() {
	gameState.playing = true;

	gameState.initting = true;

	if (STEAM_ENABLED && steamState.login == 2) {
		console.debug("Syncing Steam Cloud");
		await exports.Steam.DownloadSteamCloud();
	}

	console.debug("Init...");
	const before = performance.now();

	await exports.CelesteLoader.Init();

	// run some frames for seamless transition
	for (let i = 0; i < SEAMLESSCOUNT; i++) {
		console.debug(`SeamlessInit${i}...`);
		if (!await exports.CelesteLoader.RunAFrame()) throw new Error("CelesteLoader.RunOneFrame() Failed!");
	}

	const after = performance.now();
	console.debug(`Init : ${(after - before).toFixed(2)}ms`);
	gameState.initting = false;

	/*
	const main = async () => {
		const before = performance.now();
		const ret = await exports.CelesteLoader.MainLoop();
		const after = performance.now();

		gameState.timebuf.add(after - before);

		if (!ret) {
			console.debug("Cleanup...");

			gameState.timebuf.clear();

			await exports.CelesteLoader.Cleanup();
			gameState.ready = false;
			gameState.playing = false;

			return;
		}

		requestAnimationFrame(main);
	}
	requestAnimationFrame(main);
	*/

   	await exports.CelesteLoader.MainLoop();

	console.debug("Cleanup...");

	gameState.timebuf.clear();

	await exports.CelesteLoader.Cleanup();
	gameState.ready = false;
	gameState.playing = false;
}
