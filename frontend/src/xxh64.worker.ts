import { h64 as XXH64 } from "xxhashjs";

self.onmessage = ({ data: { buf } }) => {
	let hash = XXH64();
	hash.init(0);
	hash.update(buf);
	let out = hash.digest();

	self.postMessage({ digest: out.toString(16).toUpperCase().padStart(16, "0") })
};
