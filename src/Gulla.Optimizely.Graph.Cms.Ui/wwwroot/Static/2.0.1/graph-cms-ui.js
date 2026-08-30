(function () {
    'use strict';

    var state = {
        site: null,
        lang: null,
        slot: 'one',
        tab: null,
        collections: null,
        collectionId: null,
        pinned: null,
        pinnedNames: {},
        synonyms: null,
        targetResolve: null,
        editingKey: null
    };

    function $(id) { return document.getElementById(id); }

    function api(path) { return '/GraphCmsUi/api' + path; }

    // Pinned items live in a collection, addressed by Graph's collection id. The site is not
    // part of this — the collection already encodes which site it belongs to.
    function qs(extra) {
        var params = new URLSearchParams();
        if (state.collectionId) params.set('collection', state.collectionId);
        if (state.lang) params.set('lang', state.lang);
        if (extra) for (var k in extra) params.set(k, extra[k]);
        return params.toString();
    }

    // Collection management is per site, so those calls carry the site instead.
    function siteQs(extra) {
        var params = new URLSearchParams();
        if (state.site) params.set('site', state.site);
        if (extra) for (var k in extra) params.set(k, extra[k]);
        return params.toString();
    }

    function selectedCollection() {
        var id = state.collectionId;
        return (state.collections || []).filter(function (c) { return c.id === id; })[0] || null;
    }

    // Synonyms are scoped by language and slot only — Graph has no per-site synonym list, so
    // the site the editor has selected is deliberately not part of this.
    function synQs(extra) {
        var params = new URLSearchParams();
        if (state.lang) params.set('lang', state.lang);
        if (state.slot) params.set('slot', state.slot);
        if (extra) for (var k in extra) params.set(k, extra[k]);
        return params.toString();
    }

    // Rendered by @Html.AntiForgeryToken() in the view. Only the multipart import needs it:
    // that content type is the one a cross-origin page can post without a CORS preflight.
    function antiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // ------- Tabs -------

    function activateTab(name) {
        state.tab = name;

        var tabs = document.querySelectorAll('.gulla-tab');
        tabs.forEach(function (t) {
            var active = t.dataset.tab === name;
            t.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        document.querySelectorAll('.gulla-tab-panel').forEach(function (p) { p.hidden = true; });
        var panel = $('gulla-tab-' + name);
        if (panel) panel.hidden = false;

        // Synonyms are per language and slot only, never per site, and they don't live in
        // collections — showing either picker there would promise a scope that doesn't exist.
        var synonyms = name === 'synonyms';
        var sitePicker = $('gulla-site-picker');
        if (sitePicker) sitePicker.hidden = synonyms;
        var collectionPicker = $("gulla-collection-picker");
        if (collectionPicker) collectionPicker.hidden = synonyms;
        var slotPicker = $("gulla-slot-picker");
        if (slotPicker) slotPicker.hidden = !synonyms;

        loadActiveTab();
    }

    // The pinned form's hint names the language a non-all-languages pin will land in.
    function renderPinnedLangName() {
        var el = $('gulla-pinned-lang-name');
        if (el) el.textContent = state.lang || 'the selected language';
    }

    // Refresh the visible tab's data. Cheap for our list sizes, and it guarantees the list
    // matches what's currently in Graph. The hidden tab reloads when it's activated, so
    // there's no reason to fetch it now.
    function loadActiveTab() {
        if (!state.lang) return;
        if (state.tab === 'synonyms') return loadSynonyms();
        if (!state.site) return;
        // The collection list has to exist before anything can be listed out of one.
        return state.collections === null ? loadCollections() : loadPinned();
    }

    // ------- Collections -------

    // A pinned result belongs to a collection, and a collection belongs to a site (the site is
    // the suffix of its key). The default collection is created server-side on first list, so
    // this never comes back empty.
    function loadCollections() {
        state.collections = null;
        renderCollectionInfo();

        return fetch(api('/pinned/collections?' + siteQs()))
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) {
                state.collections = items || [];

                // Keep the current selection across a refresh when it still exists; otherwise
                // fall back to the default, which the server sorts first.
                var stillThere = state.collections.some(function (c) { return c.id === state.collectionId; });
                if (!stillThere) {
                    var def = state.collections.filter(function (c) { return c.isDefault; })[0];
                    state.collectionId = (def || state.collections[0] || {}).id || null;
                }

                renderCollectionSelect();
                renderCollectionInfo();
                state.pinned = null;
                return loadPinned();
            });
    }

    function renderCollectionSelect() {
        var select = $('gulla-collection-select');
        if (!select) return;

        select.innerHTML = (state.collections || []).map(function (c) {
            return '<option value="' + escapeHtml(c.id) + '"' +
                (c.id === state.collectionId ? ' selected' : '') + '>' +
                escapeHtml(c.name) + '</option>';
        }).join('');

        // The default collection is recreated on the next load, so deleting it would only
        // empty it. The server refuses too; this just keeps the button honest.
        var current = selectedCollection();
        var del = $('gulla-collection-delete');
        if (del) {
            del.disabled = !current || current.isDefault;
            del.title = del.disabled
                ? 'The default collection cannot be deleted'
                : 'Delete this collection and all its pinned results';
        }
    }

    // The key — not the id — is what Graph matches in a GraphQL `pinned: { collections: [...] }`
    // argument. Passing the id there returns zero results, silently, so the key is what gets
    // shown first and given the copy button.
    function renderCollectionInfo() {
        var box = $('gulla-collection-info');
        if (!box) return;

        var c = selectedCollection();
        if (!c) {
            box.hidden = true;
            box.innerHTML = '';
            return;
        }

        box.hidden = false;
        box.innerHTML =
            '<div class="gulla-info__row">' +
                '<span class="gulla-info__label">Collection key</span>' +
                '<code class="gulla-info__value">' + escapeHtml(c.key) + '</code>' +
                '<button type="button" class="gulla-button gulla-button--small" data-copy="' + escapeHtml(c.key) + '">Copy</button>' +
                '<span class="gulla-info__hint">Use this in <code>pinned: { collections: [&hellip;] }</code></span>' +
            '</div>' +
            '<div class="gulla-info__row">' +
                '<span class="gulla-info__label">Collection id</span>' +
                '<code class="gulla-info__value">' + escapeHtml(c.id) + '</code>' +
                '<button type="button" class="gulla-button gulla-button--small" data-copy="' + escapeHtml(c.id) + '">Copy</button>' +
                '<span class="gulla-info__hint">For Graph’s REST API</span>' +
            '</div>' +
            '<pre class="gulla-info__snippet">' + escapeHtml(
                'pinned: { phrase: $searchText, collections: ["' + c.key + '"] }') + '</pre>';
    }

    function addCollection() {
        var name = prompt('Name for the new collection (letters, digits and dashes):', '');
        if (name === null) return;
        name = name.trim();
        if (!name) return;

        fetch(api('/pinned/collections?' + siteQs()), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: name })
        }).then(function (r) {
            if (!r.ok) {
                return r.text().then(function (t) {
                    alert('Failed to create the collection.' + (t ? '\n\n' + t : ''));
                });
            }
            return r.json().then(function (created) {
                state.collectionId = created.id;
                return loadCollections();
            });
        });
    }

    function deleteCollection() {
        var c = selectedCollection();
        if (!c || c.isDefault) return;

        var count = (state.pinned || []).length;
        var warning = 'Delete the collection "' + c.name + '"?\n\n' +
            'This permanently removes the collection and every pinned result in it' +
            (count ? ' (' + count + ' in the current language, possibly more in others)' : '') +
            '.\n\nAny GraphQL query still passing "' + c.key + '" will stop matching.';
        if (!confirm(warning)) return;

        fetch(api('/pinned/collections/' + encodeURIComponent(c.id) + '?' + siteQs()), { method: 'DELETE' })
            .then(function (r) {
                if (!r.ok) {
                    return r.text().then(function (t) {
                        alert('Failed to delete the collection.' + (t ? '\n\n' + t : ''));
                    });
                }
                state.collectionId = null;
                return loadCollections();
            });
    }

    function bindCollections() {
        var select = $('gulla-collection-select');
        if (select) {
            select.addEventListener('change', function () {
                state.collectionId = select.value;
                state.pinned = null;
                state.editingKey = null;
                renderCollectionSelect();
                renderCollectionInfo();
                loadPinned();
            });
        }

        var add = $('gulla-collection-add');
        if (add) add.addEventListener('click', addCollection);

        var del = $('gulla-collection-delete');
        if (del) del.addEventListener('click', deleteCollection);

        var info = $('gulla-collection-info');
        if (info) {
            info.addEventListener('click', function (e) {
                var btn = e.target.closest('[data-copy]');
                if (!btn) return;
                copyToClipboard(btn.getAttribute('data-copy'), btn);
            });
        }
    }

    // navigator.clipboard needs a secure context; the CMS is usually on https but a local
    // dev site on plain http is common enough to be worth the fallback.
    function copyToClipboard(text, btn) {
        var done = function () {
            var original = btn.textContent;
            btn.textContent = 'Copied';
            setTimeout(function () { btn.textContent = original; }, 1200);
        };

        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(done, function () { legacyCopy(text, done); });
        } else {
            legacyCopy(text, done);
        }
    }

    function legacyCopy(text, done) {
        var area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'absolute';
        area.style.left = '-9999px';
        document.body.appendChild(area);
        area.select();
        try { document.execCommand('copy'); done(); } catch (e) { /* nothing useful to say */ }
        document.body.removeChild(area);
    }

    // ------- Pinned Results -------

    // Graph stores one phrase per pinned item, so a pinned result covering several phrases is several
    // items. Items that agree on everything but the phrase — same target, language, priority and
    // active state — are the same pinned result as far as the editor is concerned, so they are grouped
    // back into one row. Differ in any of those and they are genuinely separate pinned results.
    // language null means "all locales" and language "" means the NEUTRAL locale — two
    // different stored values, so they must not collapse into the same group key.
    // "*" can never appear in an ISO language code, so this cannot collide with a real one.
    function langKeyOf(item) {
        return item.language == null ? '*all' : item.language;
    }

    function isAllLanguages(item) {
        return item.language == null;
    }

    function groupKeyOf(item) {
        return [
            item.targetKey || "",
            langKeyOf(item),
            item.priority,
            item.isActive === false ? "off" : "on"
        ].join("|");
    }

    function pinnedGroups() {
        var byKey = {};
        var ordered = [];

        (state.pinned || []).forEach(function (item) {
            var key = groupKeyOf(item);
            if (!byKey[key]) {
                byKey[key] = { key: key, sample: item, items: [] };
                ordered.push(byKey[key]);
            }
            byKey[key].items.push(item);
        });

        return ordered;
    }

    function groupByKey(key) {
        return pinnedGroups().filter(function (g) { return g.key === key; })[0];
    }

    function groupPhrases(group) {
        return group.items.map(function (i) { return (i.phrases || '').trim(); }).filter(Boolean);
    }

    function splitPhrases(value) {
        var seen = {};
        return (value || '').split(',').map(function (p) { return p.trim(); }).filter(function (p) {
            if (!p) return false;
            var k = p.toLowerCase();
            if (seen[k]) return false;
            seen[k] = true;
            return true;
        });
    }

    function renderPinned() {
        var container = $('gulla-pinned-list');
        if (!container) return;

        if (state.pinned === null) {
            container.innerHTML = '<div class="gulla-list__empty">Loading&hellip;</div>';
            return;
        }

        var filter = ($('gulla-pinned-filter').value || '').toLowerCase();
        var groups = pinnedGroups().filter(function (g) {
            return !filter || groupPhrases(g).some(function (p) { return p.toLowerCase().indexOf(filter) >= 0; });
        });

        if (!groups.length) {
            container.innerHTML = '<div class="gulla-list__empty">There are no pinned results yet</div>';
            return;
        }

        container.innerHTML = groups.map(function (group) {
            var item = group.sample;
            var chips = groupPhrases(group).map(function (p) {
                return '<span class="gulla-chip">' + escapeHtml(p) + '</span>';
            }).join('');
            var resolved = state.pinnedNames[item.targetKey];
            var title = resolved ? resolved.name : item.targetKey;
            // The resolved URL is site-relative, so it works as-is from the admin page.
            // Opened in a new tab to keep the editor's place in the CMS shell.
            var urlLine = resolved && resolved.url
                ? '<div class="gulla-list__row-url"><a href="' + escapeHtml(resolved.url) + '" target="_blank" rel="noopener">' + escapeHtml(resolved.url) + '</a></div>'
                : '';
            var editing = group.key === state.editingKey;
            var disabled = item.isActive === false;
            var key = escapeHtml(group.key);
            return '<div class="gulla-list__row' + (editing ? ' gulla-list__row--editing' : '') + (disabled ? ' gulla-list__row--disabled' : '') + '">' +
                '<div class="gulla-list__row-actions">' +
                '<button class="gulla-button" data-toggle-pinned="' + key + '">' + (disabled ? 'Enable' : 'Disable') + '</button>' +
                '<button class="gulla-button" data-edit-pinned="' + key + '">Edit</button>' +
                '<button class="gulla-button" data-delete-pinned="' + key + '">Delete</button>' +
                '</div>' +
                '<div class="gulla-list__row-title">' + escapeHtml(title) +
                (disabled ? ' <span class="gulla-badge">Disabled</span>' : '') + '</div>' +
                urlLine +
                '<div class="gulla-list__row-body">Language: ' +
                    (isAllLanguages(item)
                        ? '<span class="gulla-badge gulla-badge--all">All languages</span>'
                        : escapeHtml(item.language || 'neutral')) +
                    ' &middot; Priority: ' + escapeHtml(item.priority) + '</div>' +
                '<div class="gulla-list__chips">' + chips + '</div>' +
                '</div>';
        }).join('');
    }

    function loadPinned() {
        if (!state.collectionId) {
            state.pinned = [];
            renderPinned();
            return Promise.resolve();
        }

        return fetch(api('/pinned?' + qs()))
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) {
                state.pinned = items || [];
                renderPinned();
                return resolvePinnedNames();
            });
    }

    function resolvePinnedNames() {
        var guids = state.pinned.map(function (p) { return p.targetKey; }).filter(Boolean);
        var unique = guids.filter(function (g, i) { return guids.indexOf(g) === i; });
        var missing = unique.filter(function (g) { return !state.pinnedNames[g]; });
        if (!missing.length) { renderPinned(); return; }

        Promise.all(missing.map(function (g) {
            return fetch(api('/pinned/resolve-content') + '?guid=' + encodeURIComponent(g))
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; });
        })).then(function (results) {
            results.forEach(function (r) {
                if (r && r.contentGuid) state.pinnedNames[r.contentGuid] = r;
            });
            renderPinned();
        });
    }

    function bindPinned() {
        var form = $('gulla-pinned-form');
        if (!form) return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            // Picking content kicks off a server round-trip to turn the content link into a
            // GUID. Wait for it, so submitting straight after a pick isn't mistaken for
            // "nothing selected".
            (state.targetResolve || Promise.resolve()).then(function () { savePinned(); });
        });

        $('gulla-pinned-cancel').addEventListener('click', function () { clearPinnedForm(); });

        $('gulla-pinned-list').addEventListener('click', function (e) {
            var edit = e.target.closest('[data-edit-pinned]');
            if (edit) {
                beginEditPinned(edit.getAttribute('data-edit-pinned'));
                return;
            }

            var toggle = e.target.closest('[data-toggle-pinned]');
            if (toggle) {
                togglePinned(toggle.getAttribute('data-toggle-pinned'));
                return;
            }

            var del = e.target.closest('[data-delete-pinned]');
            if (del) {
                deletePinnedGroup(del.getAttribute('data-delete-pinned'));
            }
        });

        $('gulla-pinned-filter').addEventListener('input', renderPinned);

        bindContentPicker();
    }

    // ------- Add / edit form -------

    function beginEditPinned(key) {
        var group = groupByKey(key);
        if (!group) return;

        state.editingKey = key;
        $('gulla-pinned-phrases').value = groupPhrases(group).join(', ');
        $('gulla-pinned-priority').value = group.sample.priority || 1;
        $("gulla-pinned-all-langs").checked = isAllLanguages(group.sample);

        // The picker can't be pre-selected — <optimizely-content-tree> always mounts empty and
        // exposes no way to seed it — so the current target is shown as text instead. Leaving
        // it alone keeps that target; picking something new overwrites it.
        resetContentPicker();
        $('gulla-pinned-target').value = group.sample.targetKey || '';
        showCurrentTarget(group.sample.targetKey);

        setFormMode('edit');
        $('gulla-pinned-form').scrollIntoView({ block: 'nearest' });
        renderPinned();
    }

    function clearPinnedForm() {
        state.editingKey = null;
        $('gulla-pinned-form').reset();
        resetContentPicker();
        setFormMode('add');
        renderPinned();
    }

    function setFormMode(mode) {
        $('gulla-pinned-submit').textContent = mode === 'edit' ? 'Save' : 'Add pinned result';
    }

    function showCurrentTarget(targetKey) {
        var line = $('gulla-pinned-target-current');
        if (!line) return;
        if (!targetKey) {
            line.hidden = true;
            line.textContent = '';
            return;
        }
        var resolved = state.pinnedNames[targetKey];
        line.textContent = 'Currently: ' + (resolved ? resolved.name : targetKey) +
            ' — pick again only if you want to change it';
        line.hidden = false;
    }

    // ------- Writing to Graph -------

    // Graph has no partial update, so every write sends the whole item. Each call resolves to
    // true/false rather than rejecting, so a sequence of writes can stop at the first failure.
    function sendPinned(method, url, body, failureMessage) {
        return fetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        }).then(function (r) {
            if (r.ok) return true;
            // Graph's own message is the useful part here — a duplicate phrase/target/
            // language combination comes back as a 409 explaining exactly that.
            return r.text().then(function (text) {
                alert(failureMessage + (text ? '\n\n' + text : ''));
                return false;
            });
        });
    }

    function deletePinnedItem(id) {
        return fetch(api('/pinned/' + encodeURIComponent(id) + '?' + qs()), { method: 'DELETE' })
            .then(function (r) {
                if (r.ok) return true;
                alert('Failed to delete a pinned result.');
                return false;
            });
    }

    function runSequential(steps) {
        return steps.reduce(function (chain, step) {
            return chain.then(function (ok) { return ok ? step() : false; });
        }, Promise.resolve(true));
    }

    function savePinned() {
        var phrases = splitPhrases($('gulla-pinned-phrases').value);
        var targetKey = $('gulla-pinned-target').value.trim();

        if (!phrases.length) {
            alert('Please enter at least one phrase.');
            return;
        }
        if (!targetKey) {
            alert('Please pick a target content item.');
            return;
        }

        var group = state.editingKey ? groupByKey(state.editingKey) : null;
        var allLangs = $('gulla-pinned-all-langs').checked;
        var shared = {
            targetKey: targetKey,
            // null is Graph's "every locale" value. Anything else pins to that language only.
            language: allLangs ? null : state.lang,
            priority: parseInt($('gulla-pinned-priority').value, 10) || 1,
            // The form has no field for this, so carry the existing value through — editing a
            // disabled pinned result must not quietly switch it back on.
            isActive: group ? group.sample.isActive !== false : true
        };
        function bodyFor(phrase) {
            return Object.assign({ phrases: phrase }, shared);
        }

        if (!group) {
            // The API splits a comma-separated list into one item per phrase for us.
            sendPinned('POST', api('/pinned?' + qs()), bodyFor(phrases.join(',')), 'Failed to add pinned result.')
                .then(finishSave);
            return;
        }

        runSequential(reconcileGroup(group, phrases, bodyFor)).then(finishSave);
    }

    // A phrase list is several writes, so a failure can leave some of them applied. Reload
    // either way and only clear the form when everything went through.
    function finishSave(ok) {
        if (ok) clearPinnedForm();
        loadPinned();
    }

    // Turns the group's existing items into the requested phrase list with the fewest writes:
    // a phrase that is still there updates its item in place, a leftover item is re-used for a
    // phrase that has no item yet (so an edited phrase keeps its id), and only then does
    // anything get deleted or created. Updates run before deletes and creates so that shrinking
    // a group never leaves a duplicate behind mid-way.
    function reconcileGroup(group, phrases, bodyFor) {
        var spare = group.items.slice();
        var updates = [];
        var pending = [];

        phrases.forEach(function (phrase) {
            var i = spare.findIndex(function (item) {
                return (item.phrases || '').trim().toLowerCase() === phrase.toLowerCase();
            });
            if (i >= 0) {
                updates.push(updateStep(spare.splice(i, 1)[0].id, phrase, bodyFor));
            } else {
                pending.push(phrase);
            }
        });

        while (pending.length && spare.length) {
            updates.push(updateStep(spare.shift().id, pending.shift(), bodyFor));
        }

        var deletes = spare.map(function (item) {
            return function () { return deletePinnedItem(item.id); };
        });
        var creates = pending.map(function (phrase) {
            return function () {
                return sendPinned('POST', api('/pinned?' + qs()), bodyFor(phrase), 'Failed to add a phrase to the pinned result.');
            };
        });

        return updates.concat(deletes, creates);
    }

    function updateStep(id, phrase, bodyFor) {
        return function () {
            return sendPinned('PUT', api('/pinned/' + encodeURIComponent(id) + '?' + qs()), bodyFor(phrase),
                'Failed to save the pinned result.');
        };
    }

    function togglePinned(key) {
        var group = groupByKey(key);
        if (!group) return;

        var enabling = group.sample.isActive === false;
        var steps = group.items.map(function (item) {
            return function () {
                return sendPinned('PUT', api('/pinned/' + encodeURIComponent(item.id) + '?' + qs()), {
                    phrases: item.phrases,
                    targetKey: item.targetKey,
                    language: item.language,
                    priority: item.priority,
                    isActive: enabling
                }, enabling ? 'Failed to enable the pinned result.' : 'Failed to disable the pinned result.');
            };
        });

        runSequential(steps).then(function () { loadPinned(); });
    }

    function deletePinnedGroup(key) {
        var group = groupByKey(key);
        if (!group) return;

        var count = group.items.length;
        var what = count > 1 ? 'this pinned result and all ' + count + ' of its phrases' : 'this pinned result';
        if (!confirm('Delete ' + what + '?')) return;

        var steps = group.items.map(function (item) {
            return function () { return deletePinnedItem(item.id); };
        });

        runSequential(steps).then(function () {
            if (state.editingKey === key) clearPinnedForm();
            loadPinned();
        });
    }

    // ------- Content picker (<optimizely-content-tree> web component) -------

    // The component is registered by Optimizely's optimizely-web-components.js (pulled in by
    // @Html.RegisterOptimizelyWebComponents() in the view). It renders its own selected-item
    // label and "Select content..." button, and reports a pick by dispatching an
    // `onNodeSelected` CustomEvent *on the element itself* — that is the only way to read the
    // selection, the component exposes no value property. The event detail is a content tree
    // node, `{ name, contentLink, ... }`, so we resolve the content link server-side to the
    // GUID Graph stores as the pinned result's target.
    var CONTENT_TREE_ID = 'content-tree';

    function bindContentPicker() {
        var tree = $(CONTENT_TREE_ID);
        if (!tree) return;
        tree.addEventListener('onNodeSelected', function (e) {
            var node = e.detail;
            if (!node || !node.contentLink) {
                clearPickerSelection();
                return;
            }
            applyPickedContent(node.contentLink);
        });
    }

    function applyPickedContent(contentRef) {
        state.targetResolve = fetch(api('/pinned/resolve-content') + '?contentLink=' + encodeURIComponent(contentRef), { credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (resolved) {
                if (!resolved || !resolved.contentGuid) {
                    alert('Could not resolve the selected content to a GUID.');
                    clearPickerSelection();
                    return;
                }
                $('gulla-pinned-target').value = resolved.contentGuid;
                // A fresh pick replaces whatever the pinned result pointed at before.
                showCurrentTarget(null);
            })
            .catch(function () { clearPickerSelection(); });
        return state.targetResolve;
    }

    function clearPickerSelection() {
        $('gulla-pinned-target').value = '';
        state.targetResolve = null;
        showCurrentTarget(null);
    }

    // The component holds its selection in React state, so form.reset() leaves the previous
    // pick on screen with an empty hidden field behind it. Swapping in a fresh element
    // re-mounts it with no selection.
    function resetContentPicker() {
        var tree = $(CONTENT_TREE_ID);
        clearPickerSelection();
        if (!tree) return;
        var fresh = document.createElement('optimizely-content-tree');
        fresh.id = CONTENT_TREE_ID;
        tree.replaceWith(fresh);
        bindContentPicker();
    }

    // ------- Synonyms -------

    function directionIcon(bidi) {
        return '<span class="gulla-direction-icon">' + (bidi ? '&lt;&gt;' : '&gt;') + '</span>';
    }

    function renderSynonyms() {
        var tbody = document.querySelector('#gulla-syn-list tbody');
        if (!tbody) return;

        if (state.synonyms === null) {
            tbody.innerHTML = '<tr class="gulla-table__empty"><td colspan="4">Loading&hellip;</td></tr>';
            return;
        }

        var filter = ($('gulla-syn-filter').value || '').toLowerCase();
        var rows = state.synonyms.filter(function (s) {
            if (!filter) return true;
            var blob = (s.phrases || []).join(',') + ' ' + (s.synonym || '');
            return blob.toLowerCase().indexOf(filter) >= 0;
        });

        if (!rows.length) {
            tbody.innerHTML = '<tr class="gulla-table__empty"><td colspan="4">No synonyms yet</td></tr>';
            return;
        }

        tbody.innerHTML = rows.map(function (s) {
            return '<tr>' +
                '<td>' + escapeHtml((s.phrases || []).join(', ')) + '</td>' +
                '<td class="gulla-table__direction">' + directionIcon(s.bidirectional) + '</td>' +
                '<td>' + escapeHtml(s.synonym || '') + '</td>' +
                '<td><button class="gulla-button" data-delete-syn="' + escapeHtml(s.rowKey) + '">Delete</button></td>' +
                '</tr>';
        }).join('');
    }

    function loadSynonyms() {
        return fetch(api('/synonyms?' + synQs()))
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) { state.synonyms = items || []; renderSynonyms(); });
    }

    // A fan-out writes one language at a time and any of them can fail on its own, so the
    // outcome is a tally rather than a yes/no. Silence would be wrong here: "added to 5 of 7"
    // is the only honest thing to say when two languages didn't take.
    function reportFanOut(res) {
        var added = res.added || [];
        var skipped = res.skipped || [];
        var failed = res.failed || [];
        var total = added.length + skipped.length + failed.length;

        var lines = ['Added to ' + added.length + ' of ' + total + ' languages.'];
        if (skipped.length) {
            lines.push('Already present in: ' + skipped.join(', '));
        }
        if (failed.length) {
            lines.push('Failed in: ' + failed.map(function (f) { return f.language; }).join(', '));
            lines.push('');
            lines.push(failed[0].error);
        }
        alert(lines.join('\n'));
    }

    // The synonyms counterpart of renderCollectionInfo, deliberately the same shape: the value
    // a query has to reference, with a copy button, then the snippet it goes into.
    // `synonyms` sits INSIDE the field filter, not beside `where` — putting it at query level
    // is a syntax error, so the snippet shows the nesting rather than the argument alone.
    function renderSlotInfo() {
        var box = $('gulla-syn-info');
        if (!box) return;

        var slot = (state.slot || 'one').toUpperCase();
        var lang = state.lang || '—';

        box.innerHTML =
            '<div class="gulla-info__row">' +
                '<span class="gulla-info__label">Synonym slot</span>' +
                '<code class="gulla-info__value">' + escapeHtml(slot) + '</code>' +
                '<button type="button" class="gulla-button gulla-button--small" data-copy="synonyms: [' + escapeHtml(slot) + ']">Copy</button>' +
                '<span class="gulla-info__hint">Graph gives each language two slots; the query picks which one applies</span>' +
            '</div>' +
            '<div class="gulla-info__row">' +
                '<span class="gulla-info__label">Language</span>' +
                '<code class="gulla-info__value">' + escapeHtml(lang) + '</code>' +
                '<span class="gulla-info__hint">Shared by every site on this Graph instance. Unlike pinned results, synonyms cannot be scoped per site</span>' +
            '</div>' +
            '<pre class="gulla-info__snippet">' + escapeHtml(
                'where: { MainBody: { contains: $searchText, synonyms: [' + slot + '] } }') + '</pre>';
    }

    function bindSynonyms() {
        var form = $('gulla-syn-form');
        if (!form) return;

        var info = $('gulla-syn-info');
        if (info) {
            info.addEventListener('click', function (e) {
                var btn = e.target.closest('[data-copy]');
                if (btn) copyToClipboard(btn.getAttribute('data-copy'), btn);
            });
        }

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var body = {
                phrases: $('gulla-syn-phrases').value.trim(),
                synonym: $('gulla-syn-synonym').value.trim(),
                bidirectional: $('gulla-syn-bidi').checked
            };
            if (!body.phrases || !body.synonym) return;

            var allLangs = $('gulla-syn-all-langs').checked;

            fetch(api('/synonyms?' + synQs(allLangs ? { allLanguages: 'true' } : null)), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            }).then(function (r) {
                // The server forwards Graph's own status and message, and answers a duplicate
                // with a 409 that says so — worth showing rather than swallowing.
                if (!r.ok) {
                    return r.text().then(function (t) {
                        alert('Failed to add synonym.' + (t ? '\n\n' + t : ''));
                    });
                }
                return r.json().then(function (res) {
                    if (allLangs) reportFanOut(res);
                    form.reset();
                    loadSynonyms();
                });
            });
        });

        document.querySelector('#gulla-syn-list').addEventListener('click', function (e) {
            var btn = e.target.closest('[data-delete-syn]');
            if (!btn) return;
            var key = btn.getAttribute('data-delete-syn');
            if (!confirm('Delete this synonym?')) return;
            // The row key travels as a query parameter: it is built from editor text, and a
            // phrase containing a slash would encode to %2F and be rejected in a path segment.
            fetch(api('/synonyms?' + synQs({ rowKey: key })), { method: 'DELETE' })
                .then(function (r) {
                    if (!r.ok) return r.text().then(function (t) { alert('Failed to delete the synonym.' + (t ? '\n\n' + t : '')); });
                    loadSynonyms();
                });
        });

        $('gulla-syn-filter').addEventListener('input', renderSynonyms);

        $('gulla-syn-import-file').addEventListener('change', function (e) {
            var file = e.target.files && e.target.files[0];
            if (!file) return;
            var fd = new FormData();
            fd.append('file', file);
            fd.append('__RequestVerificationToken', antiForgeryToken());
            fetch(api('/synonyms/import?' + synQs()), { method: 'POST', body: fd })
                .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
                .then(function (res) {
                    // res.total is what was in the file; res.skipped were already present.
                    alert(res.skipped
                        ? 'Imported ' + res.imported + ' of ' + res.total + ' synonyms. ' +
                          res.skipped + ' already existed and were skipped.'
                        : 'Imported ' + res.imported + ' synonyms.');
                    loadSynonyms();
                })
                .catch(function () { alert('Import failed.'); });
            e.target.value = '';
        });

        $('gulla-syn-export').addEventListener('click', function (e) {
            e.preventDefault();
            window.location.href = api('/synonyms/export?' + synQs());
        });
    }

    // ------- Boot -------

    function init() {
        var siteSelect = $('gulla-site-select');
        var langSelect = $('gulla-lang-select');
        var slotSelect = $('gulla-syn-slot');
        state.site = siteSelect ? siteSelect.value : null;
        state.lang = langSelect ? langSelect.value : null;
        state.slot = (slotSelect && slotSelect.value) || 'one';

        // Changing site or language invalidates the visible list; the other tab picks up the
        // new selection when it is activated. A different site means a different set of
        // collections, so those have to be re-fetched rather than filtered.
        if (siteSelect) siteSelect.addEventListener('change', function () {
            state.site = siteSelect.value;
            state.collections = null;
            state.collectionId = null;
            state.pinned = null;
            state.editingKey = null;
            loadActiveTab();
        });
        if (langSelect) langSelect.addEventListener('change', function () { state.lang = langSelect.value; state.pinned = null; state.synonyms = null; renderPinnedLangName(); renderSlotInfo(); loadActiveTab(); });
        if (slotSelect) slotSelect.addEventListener('change', function () { state.slot = slotSelect.value; state.synonyms = null; renderSlotInfo(); loadSynonyms(); });

        document.querySelectorAll('.gulla-tab').forEach(function (tab) {
            tab.addEventListener('click', function () { activateTab(tab.dataset.tab); });
        });

        renderPinnedLangName();
        renderSlotInfo();
        bindCollections();
        bindPinned();
        bindSynonyms();

        // Last, so the handlers above are in place: activateTab loads the tab it shows.
        activateTab((window.gullaGraphUi && window.gullaGraphUi.initialTab) || 'pinned-results');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
