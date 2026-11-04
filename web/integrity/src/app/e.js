const fs=require('fs');
global.edit=(f,pairs)=>{let s=fs.readFileSync(f,'utf8').replace(/\r\n/g,'\n');for(const [a,b] of pairs){if(!s.includes(a))throw new Error(f+' missing '+a.slice(0,70));s=s.replace(a,()=>b);}fs.writeFileSync(f,s);};
edit('shared/items-editor.ts',[
["import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';","import { ChangeDetectionStrategy, Component, OnInit, inject, input, model } from '@angular/core';\nimport { MachineKinds } from './machine-kinds';"],
[`        <div class="row3">
          <div class="field">
            <label [for]="'n' + $index">Name</label>
            <input [id]="'n' + $index" type="text" [disabled]="disabled()" [value]="it.name" (input)="set($index, { name: $any($event.target).value })" />
          </div>
          <div class="field">
            <label [for]="'q' + $index">Quantity</label>
            <input [id]="'q' + $index" type="number" min="1" max="10000" [disabled]="disabled()" [value]="it.quantity" (input)="set($index, { quantity: +$any($event.target).value || 1 })" />
          </div>
          <div class="field">
            <label [for]="'s' + $index">Serial number</label>
            <input [id]="'s' + $index" type="text" [disabled]="disabled()" [value]="it.serialNumber ?? ''" (input)="set($index, { serialNumber: $any($event.target).value })" />
          </div>
        </div>`,`        @let gear = kinds.isGear(it.name, it.type);
        <div class="row3">
          <div class="field">
            <label [for]="'n' + $index">Name</label>
            <input [id]="'n' + $index" type="text" [disabled]="disabled()" [value]="it.name" (input)="set($index, { name: $any($event.target).value })" />
          </div>
          @if (gear) {
            <div class="field">
              <label [for]="'q' + $index">Quantity</label>
              <input [id]="'q' + $index" type="number" min="1" max="10000" [disabled]="disabled()" [value]="it.quantity" (input)="set($index, { quantity: +$any($event.target).value || 1 })" />
              <small>The inspector divides it into certificates on site and writes the serial numbers then.</small>
            </div>
          } @else {
            <div class="field">
              <label [for]="'s' + $index">Serial number</label>
              <input [id]="'s' + $index" type="text" [disabled]="disabled()" [value]="it.serialNumber ?? ''" (input)="set($index, { serialNumber: $any($event.target).value })" />
            </div>
          }
          <div class="field">
            <label [for]="'t' + $index">Type</label>
            <input [id]="'t' + $index" type="text" [disabled]="disabled()" [value]="it.type ?? ''" (input)="set($index, { type: $any($event.target).value })" />
          </div>
        </div>`],
[`        <div class="row3">
          <div class="field">
            <label [for]="'t' + $index">Type</label>
            <input [id]="'t' + $index" type="text" [disabled]="disabled()" [value]="it.type ?? ''" (input)="set($index, { type: $any($event.target).value })" />
          </div>
          <div class="field">
            <label [for]="'c' + $index">Capacity</label>
            <input [id]="'c' + $index" type="text" [disabled]="disabled()" [value]="it.capacity ?? ''" (input)="set($index, { capacity: $any($event.target).value })" />
          </div>
          <div class="field">
            <label [for]="'l' + $index">Where on site</label>
            <input [id]="'l' + $index" type="text" [disabled]="disabled()" [value]="it.location ?? ''" (input)="set($index, { location: $any($event.target).value })" />
          </div>
        </div>
        <div class="field">`,`        <div class="row2">
          <div class="field">
            <label [for]="'c' + $index">Capacity</label>
            <input [id]="'c' + $index" type="text" [disabled]="disabled()" [value]="it.capacity ?? ''" (input)="set($index, { capacity: $any($event.target).value })" />
          </div>
          <div class="field">
            <label [for]="'i' + $index">What the client says is wrong</label>
            <input [id]="'i' + $index" type="text" [disabled]="disabled()" [value]="it.issueDescription ?? ''" (input)="set($index, { issueDescription: $any($event.target).value })" />
          </div>
        </div>
        <div class="hidden-old">`],
["export class ItemsEditor {\n","export class ItemsEditor implements OnInit {\n  protected readonly kinds = inject(MachineKinds);\n\n  ngOnInit(): void {\n    this.kinds.load();\n  }\n\n"],
["    this.items.update((l) => l.map((it, x) => (x === i ? { ...it, ...patch } : it)));","    this.items.update((l) => l.map((it, x) => (x === i ? this.normalise({ ...it, ...patch }) : it)));"],
["  protected pick(","  /** A machine that is not a gear is one unit; a gear has no serial number at this level (the inspector writes them per certificate). */\n  private normalise(it: SaveRequestItem): SaveRequestItem {\n    return this.kinds.isGear(it.name, it.type) ? { ...it, serialNumber: null } : { ...it, quantity: 1 };\n  }\n  protected pick("],
["        ? { equipmentId: e.id, name: e.name, type: e.category ?? null, capacity: e.capacity, serialNumber: e.serialNumber, location: e.locationInsideClientLocation }","        ? { equipmentId: e.id, name: e.name, type: e.category ?? null, capacity: e.capacity, serialNumber: e.serialNumber }"],
]);
