#include <stdint.h>
#include <sys/types.h>
#include <unistd.h>
#include <sys/mman.h>
#include <sys/utsname.h>
#include <stdlib.h>
#include <stdio.h>

#include <mono/metadata/class-internals.h>
#include <mono/mini/interp/interp-internals.h>

#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wint-conversion"

int32_t _mincore(void *addr, void *length, void *vec) {
	return mincore(addr, length, vec);
}

int32_t _mkstemp(void *template) {
	return mkstemp(template);
}

void *_mmap(void *addr, void *length, int32_t prot, int32_t flags, int32_t fd, int32_t offset) {
	return mmap(addr, length, prot, flags, fd, offset);
}

int32_t _mprotect(void *addr, void *len, int32_t prot) {
	// return mprotect(addr, len, prot);
	return 0;
}

int _munmap(void *addr, size_t len) {
	return munmap(addr, len);
}

int _pipe2(int pipefd[2], int flags) {
	// return pipe2(pipefd, flags);
	return 0;
}

void *_read(int fd, void *buf, void *count) {
	return read(fd, buf, count);
}

void *_write(int fd, const void *buf, void *count) {
	return write(fd, buf, count);
}

int64_t _sysconf(int32_t name) {
	return sysconf(name);
}

int32_t _uname(void *x) {
	return uname(x);
}


MonoMethodHeader *get_header(MonoFtnDesc *mono) {
	if (!mono || !mono->method) return NULL;
	if (mono->method->klass)
		mono_class_get_image(mono->method->klass)->has_updates = FALSE;
	MonoMethodHeader *header = mono_method_get_header(mono->method);
	if (mono->method->klass)
		mono_class_get_image(mono->method->klass)->has_updates = TRUE;
	return header;
}

const void *magictranslate(MonoFtnDesc *mono) {
	MonoMethodHeader *header = get_header(mono);
	if (!header) return NULL;

	const void *code = header->code;

	mono_metadata_free_mh(header);
	return code;
}

uint32_t magictranslatelen(MonoFtnDesc *mono) {
	MonoMethodHeader *header = get_header(mono);
	if (!header) return 0;

	uint32_t code_size = header->code_size;

	mono_metadata_free_mh(header);
	return code_size;
}

void magicinvalidate(MonoFtnDesc *mono) {
	if (mono->interp_method)
		((InterpMethod*)mono->interp_method)->transformed = 0;
	else
		mono_interp_get_imethod(mono->method)->transformed = 0;
}

extern void hot_reload_insert_detour(MonoImage *image, guint32 token, gpointer code);
extern void hot_reload_remove_detour(MonoImage *image, guint32 token);

int magicdetour2allowed(MonoFtnDesc *mono) {
	return !mono->method->is_inflated && mono->method->wrapper_type == MONO_WRAPPER_NONE && !mono->method->sre_method;
}

void magicdetour2(MonoFtnDesc *mono, gpointer code) {
	mono_class_get_image(mono->method->klass)->has_updates = TRUE;
	hot_reload_insert_detour(mono_class_get_image(mono->method->klass), mono_metadata_token_index(mono->method->token), code);
}

void magicundetour2(MonoFtnDesc *mono) {
	hot_reload_remove_detour(mono_class_get_image(mono->method->klass), mono_metadata_token_index(mono->method->token));
}
