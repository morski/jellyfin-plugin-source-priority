import fs from 'node:fs/promises';
import vm from 'node:vm';
import assert from 'node:assert/strict';

// Dependency-free DOM/API harness: exercises the actual embedded page script.
class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.events = {}; this.style = {}; this.value = ''; }
  appendChild(child) { this.children.push(child); child.parent = this; }
  replaceChildren() { this.children = []; }
  addEventListener(name, callback) { this.events[name] = callback; }
  remove() { this.parent.children = this.parent.children.filter(c => c !== this); }
  reportValidity() { return true; }
}
const clone = v => JSON.parse(JSON.stringify(v));
const page = new Element('page');
const elements = Object.fromEntries(['SourcePriorityForm', 'priorityLibraries', 'priorityStatus', 'prioritySave', 'priorityEnabled'].map(id => [id, new Element(id)]));
page.querySelector = selector => elements[selector.slice(1)];
const libs = [
  { Name: 'Movies <unsafe>', ItemId: '11111111111111111111111111111111', Locations: ['/example'] },
  { Name: 'TV', ItemId: '22222222222222222222222222222222', Locations: ['/tv/fast', '/tv/slo'] }
];
let config = { Enabled: true, DeprioritizedPaths: ['/example/archive'], Libraries: [] };
let saves = 0, failLoad = false;
const context = vm.createContext({ document: { querySelector: () => page, createElement: tag => new Element(tag) },
  Dashboard: { showLoadingMsg() {}, hideLoadingMsg() {} },
  ApiClient: {
    async getPluginConfiguration() { return clone(config); },
    async getVirtualFolders() { if (failLoad) throw new Error('network'); return clone(libs); },
    async updatePluginConfiguration(_, value) {
      saves++; config = clone(value);
      // Server Guid serialization inserts hyphens.
      config.Libraries.forEach(l => { l.LibraryId = l.LibraryId.replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/, '$1-$2-$3-$4-$5'); });
    }
  }
});
const html = await fs.readFile(new URL('../src/configPage.html', import.meta.url), 'utf8');
new vm.Script(html.match(/<script>([\s\S]*?)<\/script>/)[1]).runInContext(context);
const all = (root, tag) => root.children.flatMap(c => [ ...(c.tag === tag ? [c] : []), ...all(c, tag) ]);
const submit = () => elements.SourcePriorityForm.events.submit({ preventDefault() {} });
await page.events.pageshow();
assert.equal(elements.prioritySave.disabled, false);
assert.equal(elements.priorityLibraries.children.length, 2);
const movieSection = elements.priorityLibraries.children[0];
assert.equal(movieSection.children[0].textContent, 'Movies <unsafe>');
let inputs = all(movieSection, 'input');
assert.deepEqual(inputs.map(i => i.value), ['/example', '0', '/example/archive', '-100']);
assert.equal(inputs[0].readOnly, true);
const tvSection = elements.priorityLibraries.children[1];
const tvInputs = all(tvSection, 'input'); tvInputs[1].value = '20'; tvInputs[3].value = '-20';
await submit();
assert.equal(saves, 1);
assert.equal(config.Libraries[1].Folders[0].Priority, 20);
assert.equal(config.Libraries[1].Folders[1].Priority, -20);
assert.equal(elements.priorityStatus.textContent, 'Priorities saved and confirmed.');
all(movieSection, 'button').find(b => b.textContent === 'Add subfolder override').events.click();
inputs = all(movieSection, 'input');
inputs[4].value = '/elsewhere'; inputs[5].value = '10';
await submit(); assert.equal(saves, 1); assert.match(elements.priorityStatus.textContent, /inside a configured/);
inputs[4].value = '/example';
await submit(); assert.equal(saves, 1); assert.match(elements.priorityStatus.textContent, /Duplicate/);
inputs[4].value = '/example/archive/special'; inputs[5].value = '50';
await submit(); assert.equal(saves, 2);
assert.equal(config.Libraries[0].Folders[2].Priority, 50);
config.Enabled = false;
await submit(); assert.equal(saves, 2); assert.match(elements.priorityStatus.textContent, /changed elsewhere/);
failLoad = true; await page.events.pageshow();
assert.equal(elements.prioritySave.disabled, true); assert.match(elements.priorityStatus.textContent, /Could not load/);
console.log('PASS: settings-page load/save, folder discovery, migration, validation, conflict and failure scenarios');
