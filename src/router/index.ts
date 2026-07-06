import { createRouter, createWebHashHistory } from 'vue-router'
import LoginView from '../views/LoginView.vue'
import ServerSelectView from '../views/ServerSelectView.vue'
import AppLayout from '../views/AppLayout.vue'
import HomeView from '../views/HomeView.vue'
import OverviewView from '../views/OverviewView.vue'
import NationView from '../views/NationView.vue'
import AccountView from '../views/AccountView.vue'
import SettingsView from '../views/SettingsView.vue'
import LeaderboardView from '../views/LeaderboardView.vue'
import MapView from '../views/MapView.vue'
import ModsView from '../views/ModsView.vue'
import SuggestModView from '../views/SuggestModView.vue'
import FeedbackView from '../views/FeedbackView.vue'

const routes = [
  {
    path: '/login',
    component: LoginView,
  },
  {
    path: '/servers',
    component: ServerSelectView,
  },
  {
    path: '/',
    component: AppLayout,
    children: [
      { path: '', redirect: '/home' },
      { path: 'home', component: HomeView },
      { path: 'overview', component: OverviewView },
      { path: 'nation', component: NationView },
      { path: 'account', component: AccountView },
      { path: 'settings', component: SettingsView },
      { path: 'mods', component: ModsView },
      { path: 'suggest-mod', component: SuggestModView },
      { path: 'feedback', component: FeedbackView },
      { path: 'leaderboard', component: LeaderboardView },
      { path: 'map', component: MapView },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/home',
  },
]

export default createRouter({
  history: createWebHashHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 }
  },
})
