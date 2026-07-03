// Shared enrichment helper — available on all pages (loaded after jQuery in _Layout)

function sleep(ms) { return new Promise(function(r){ setTimeout(r, ms); }); }

// Retries an $.ajax call on rate-limit (429 / TooManyRequests).
// Each retry uses a fresh LLM key because the server fetches a new key per request.
async function ajaxWithRetry(opts, maxRetries) {
    maxRetries = maxRetries || 4;
    for (var attempt = 1; attempt <= maxRetries; attempt++) {
        try {
            return await $.ajax(opts);
        } catch (jqXHR) {
            var status = (jqXHR && jqXHR.status) ? jqXHR.status : 0;
            var errJson = {};
            try { errJson = JSON.parse(jqXHR.responseText || '{}'); } catch (_) {}
            var errMsg = errJson.error || '';
            var isRateLimit = status === 429 ||
                (status === 500 && errMsg.toLowerCase().indexOf('toomanyrequests') >= 0);
            if (isRateLimit && attempt < maxRetries) {
                if (typeof onAjaxRetry === 'function') onAjaxRetry(attempt, maxRetries);
                await sleep(1500);
                continue;
            }
            if (attempt >= maxRetries) throw jqXHR;
            await sleep(2000);
        }
    }
}
