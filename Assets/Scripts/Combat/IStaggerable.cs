// Cible dont une attaque peut être interrompue.
//
// Rien n'interrompait les ennemis : un ennemi frappé poursuivait son anticipation et
// touchait quand même. Le stagger est ce qui donne du sens à une arme lourde et,
// surtout, ce qui récompense une parade réussie.
public interface IStaggerable {

    void Stagger(float duration);
    bool IsStaggered { get; }
}
