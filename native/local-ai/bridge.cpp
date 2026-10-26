// Stable, deliberately small C ABI. llama.cpp's version-dependent structs never cross P/Invoke.
#include "llama.h"
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>
#if defined(_WIN32)
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif
struct engine {
    llama_model *model = nullptr;
    llama_context *context = nullptr;
    std::atomic<bool> canceled{false};
    ~engine() { if(context) llama_free(context); if(model) llama_model_free(model); }
};
static thread_local std::string last_error;
API const char *tsai_error() { return last_error.c_str(); }
API void *tsai_load(const char *path, int context, int threads, int gpu_layers) {
    try {
        static std::once_flag init;
        std::call_once(init, [] { llama_backend_init(); });
        auto e = std::make_unique<engine>();
        auto mp = llama_model_default_params();
        mp.n_gpu_layers = gpu_layers;
        e->model = llama_model_load_from_file(path, mp);
        if(!e->model) { last_error = "Cannot load this GGUF model."; return nullptr; }
        auto cp = llama_context_default_params();
        cp.n_ctx = std::clamp(context, 256, 4096);
        cp.n_batch = 256;
        cp.n_threads = cp.n_threads_batch = std::clamp(threads, 1, 8);
        cp.no_perf = false;
        cp.abort_callback = [](void *p) { return static_cast<engine *>(p)->canceled.load(); };
        cp.abort_callback_data = e.get();
        e->context = llama_init_from_model(e->model, cp);
        if(!e->context) { last_error = "Cannot allocate model context; try a smaller model."; return nullptr; }
        return e.release();
    } catch(const std::exception &ex) { last_error = ex.what(); return nullptr; }
}
API void tsai_free(void *p) { delete static_cast<engine *>(p); }
API void tsai_cancel(void *p) { static_cast<engine *>(p)->canceled.store(true); }
API void tsai_reset_cancel(void *p) { static_cast<engine *>(p)->canceled.store(false); }
using piece_callback = int (*)(const char *, int, void *);
API int tsai_generate(void *p, const char *prompt, int max_tokens, int ignore_eos,
                      piece_callback callback, void *user, double *tokens_per_second) {
    try {
        auto e = static_cast<engine *>(p);
        auto vocab = llama_model_get_vocab(e->model);
        int length = (int)strlen(prompt);
        int count = -llama_tokenize(vocab, prompt, length, nullptr, 0, true, true);
        if(count <= 0 || count + max_tokens > (int)llama_n_ctx(e->context)) {
            last_error = "Prompt exceeds the model context. Start a new conversation or reduce context."; return -1;
        }
        std::vector<llama_token> tokens(count);
        if(llama_tokenize(vocab, prompt, length, tokens.data(), count, true, true) != count) return -1;
        llama_memory_clear(llama_get_memory(e->context), true);
        for(int offset = 0; offset < count; offset += 256) {
            if(e->canceled.load()) return -2;
            auto batch = llama_batch_get_one(tokens.data() + offset, std::min(256, count - offset));
            if(llama_decode(e->context, batch) != 0) { last_error = "Prompt evaluation failed."; return e->canceled.load() ? -2 : -1; }
        }
        auto sampler = std::unique_ptr<llama_sampler, decltype(&llama_sampler_free)>(llama_sampler_init_greedy(), llama_sampler_free);
        auto start = std::chrono::steady_clock::now();
        int generated = 0;
        std::vector<char> piece(256);
        for(int i = 0; i < max_tokens && !e->canceled.load(); ++i) {
            auto token = llama_sampler_sample(sampler.get(), e->context, -1);
            if(!ignore_eos && llama_vocab_is_eog(vocab, token)) break;
            int size = llama_token_to_piece(vocab, token, piece.data(), (int)piece.size(), 0, false);
            if(size < 0) { piece.resize(-size); size = llama_token_to_piece(vocab, token, piece.data(), (int)piece.size(), 0, false); }
            if(size < 0) { last_error = "Token decoding failed."; return -1; }
            ++generated;
            if(callback && callback(piece.data(), size, user) != 0) { e->canceled.store(true); break; }
            if(i + 1 < max_tokens) {
                auto batch = llama_batch_get_one(&token, 1);
                if(llama_decode(e->context, batch) != 0) { last_error = "Token evaluation failed."; return e->canceled.load() ? -2 : -1; }
            }
        }
        double seconds = std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();
        if(tokens_per_second) *tokens_per_second = generated / std::max(seconds, 0.000001);
        return e->canceled.load() ? -2 : generated;
    } catch(const std::exception &ex) { last_error = ex.what(); return -1; }
}
