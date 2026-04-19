import { Component } from '@angular/core';
import { StatusCardComponent } from './components/status-card/status-card';
import { StreamListComponent } from './components/stream-list/stream-list';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [StatusCardComponent, StreamListComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
