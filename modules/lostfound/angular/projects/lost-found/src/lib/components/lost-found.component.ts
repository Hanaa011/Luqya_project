import { Component, inject } from '@angular/core';
import { LostFoundService } from '../services/lost-found.service';

@Component({
  selector: 'lib-lost-found',
  template: ` <p>lost-found works!</p> `,
})
export class LostFoundComponent {
  protected readonly service = inject(LostFoundService);

  constructor() {
    this.service.sample().subscribe(console.log);
  }
}
