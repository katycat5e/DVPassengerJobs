using CommsRadioAPI;

namespace PassengerJobs.DebugTools.StationEditor
{
    public class NameStationState : BaseTrackLocationState
    {
        private readonly string _newId;
        private readonly int _charIndex;

        public NameStationState() : this(RaycastResult.NONE, " ", 0)
        {

        }

        protected NameStationState(RaycastResult raycast, string newId, int charIndex) :
            base(GetState(newId, charIndex), raycast)
        {
            _newId = newId;
            _charIndex = charIndex;
        }

        private static CommsRadioState GetState(string newId, int charIndex)
        {
            char[] pointer = new char[charIndex + 1];
            for (int i = 0; i < charIndex; i++)
            {
                pointer[i] = ' ';
            }
            pointer[charIndex] = '^';

            string prompt = string.Concat(newId, '\n', new string(pointer));

            return new CommsRadioState(RadioSetup.STATION_PLACER_TITLE, prompt, buttonBehaviour: DV.ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            base.OnUpdate(utility);

            var result = GetRaycastResult(utility);

            return new NameStationState(result, _newId, _charIndex);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (action == InputAction.Up)
            {
                char[] updatedId = _newId.ToCharArray();
                char current = updatedId[_charIndex];

                if (current == ' ')
                {
                    updatedId[_charIndex] = 'A';
                }
                else if (current < 'Z')
                {
                    updatedId[_charIndex] = (char)(current + 1);
                }
                return new NameStationState(_raycastResult, new string(updatedId), _charIndex);
            }

            if (action == InputAction.Down)
            {
                char[] updatedId = _newId.ToCharArray();
                char current = updatedId[_charIndex];

                if (current > 'A')
                {
                    updatedId[_charIndex] = (char)(current - 1);
                }
                else if (current == 'A')
                {
                    updatedId[_charIndex] = ' ';
                }
                return new NameStationState(_raycastResult, new string(updatedId), _charIndex);
            }

            // activate button
            if (!_raycastResult.IsTrack)
            {
                return new SelectStationState();
            }

            if (_newId[_charIndex] == ' ' || _charIndex == 2)
            {
                return new EditStationState(_newId.Trim());
            }

            return new NameStationState(_raycastResult, _newId + " ", _charIndex + 1);
        }
    }
}
