import { mount } from 'svelte';
import App from './App.svelte';
import './style.css';
import { applyScheme, initialScheme } from './lib/ui-preferences.ts';
applyScheme(initialScheme());
mount(App, { target: document.getElementById('app')! });
