<template>
  <div class="card" style="max-width: 560px">
    <h2>Nueva consulta</h2>
    <form @submit.prevent="guardar">
      <div class="form-group">
        <label>Afiliado</label>
        <select v-model="form.afiliadoId" required>
          <option value="">Seleccioná un afiliado</option>
          <option v-for="a in afiliados" :key="a.id" :value="a.id">
            {{ a.nombreCompleto }} — DNI {{ a.dni }}
          </option>
        </select>
      </div>
      <div class="form-group">
        <label>Profesional</label>
        <select v-model="form.profesionalId" required>
          <option value="">Seleccioná un profesional</option>
          <option v-for="p in profesionales" :key="p.id" :value="p.id">
            {{ p.nombreCompleto }} — {{ p.especialidad }}
          </option>
        </select>
      </div>
      <div class="form-group">
        <label>Modalidad</label>
        <select v-model="form.modalidad">
          <option value="Presencial">Presencial</option>
          <option value="Virtual">Virtual</option>
        </select>
      </div>
      <div class="form-group">
        <label>Fecha y hora</label>
        <input type="datetime-local" v-model="form.fechaHora" :min="minFecha" required />
      </div>
      <div class="form-group">
        <label>Motivo</label>
        <input type="text" v-model="form.motivo" placeholder="Motivo de la consulta" />
      </div>
      <button type="submit">Confirmar consulta</button>
    </form>
  </div>
</template>

<script>
import { consultasApi, afiliadosApi, profesionalesApi, notificar } from '../services/api'

export default {
  name: 'ConsultaNueva',
  data() {
    return {
      form: {
        afiliadoId: '',
        profesionalId: '',
        modalidad: 'Presencial',
        fechaHora: '',
        motivo: ''
      },
      afiliados: [],
      profesionales: []
    }
  },
  computed: {
    // Fecha mínima en hora local (formato datetime-local: yyyy-MM-ddTHH:mm)
    minFecha() {
      const d = new Date(Date.now() - new Date().getTimezoneOffset() * 60000)
      return d.toISOString().slice(0, 16)
    }
  },
  async mounted() {
    try {
      const [aRes, pRes] = await Promise.all([afiliadosApi.getAll(), profesionalesApi.getAll()])
      this.afiliados = aRes.data
      this.profesionales = pRes.data
    } catch {
      // El interceptor de axios ya informó el error
    }
  },
  methods: {
    async guardar() {
      if (!this.form.fechaHora || new Date(this.form.fechaHora) <= new Date()) {
        notificar("La fecha y hora de la consulta debe ser futura.")
        return
      }
      try {
        await consultasApi.create({
          afiliadoId: Number(this.form.afiliadoId),
          profesionalId: Number(this.form.profesionalId),
          modalidad: this.form.modalidad,
          fechaHora: this.form.fechaHora,
          motivo: this.form.motivo
        })
        this.$router.push('/consultas')
      } catch {
        // El interceptor de axios ya informó el error
      }
    }
  }
}
</script>
